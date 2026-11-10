using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ChestCounter;

/// <summary>
/// Small always-on-top counter window drawn with plain Win32/GDI (no WinForms needed).
/// Left-drag to move it, right-click to hide it (press O in the console to show it again).
/// Visible over Fortnite when the game runs in "windowed fullscreen" (not exclusive fullscreen).
/// </summary>
/// <summary>What the overlay shows. <see cref="Current"/> null = no match in progress.</summary>
/// <param name="Detail">Optional secondary text drawn small and grey after the value.</param>
/// <param name="Color">Value colour "#RRGGBB" set by a script add-on (null = theme colour).</param>
public sealed record OverlayRow(string Label, string Value, bool Treasure = false, string? Detail = null, string? Color = null);

/// <param name="Current">Right counter (match in progress, or last match); null = "—".</param>
/// <param name="CurrentLive">Right counter is the match in progress (gold).</param>
/// <param name="Badge">Header badge ("● EN DIRECT" / "● LIVE", "● PARTIE EN COURS" / "● MATCH IN PROGRESS") or null.</param>
/// <param name="HeaderText">Header text (null = "COFFRES"); TotalCaption: caption under the total (null = "total"). Both set by script add-ons.</param>
public sealed record OverlayView(string Total, string? Current, string CurrentCaption, bool CurrentLive, string? Badge, IReadOnlyList<OverlayRow> Rows,
    string? HeaderText = null, string? TotalCaption = null);

[SupportedOSPlatform("windows")]
public static class NativeOverlay
{
    private static Thread? _thread;
    private static IntPtr _hwnd;
    private static volatile OverlayView _view = new("0", null, OverlayModel.LastMatchCaption, false, null, Array.Empty<OverlayRow>());

    // Layout (computed by Relayout, used by Paint; both on the overlay thread).
    private static int _headerH, _bigH, _captionH, _rowH, _col1W, _col2W, _labelW;
    private static IntPtr _fontHeader, _fontCaption, _fontLabel;
    private static IntPtr _brushBorder, _brushLine;
    private static string Header => L.T("COFFRES", "CHESTS", "COFRES", "TRUHEN", "СУНДУКИ", "宝箱", "FORZIERI", "BAÚS", "SKRZYNIE", "KISTEN", "SANDIKLAR", "상자", "宝箱", "СКРИНІ", "KISTOR", "TRUHLY");
    private static string CaptionTotal => L.T("total", "total", "total", "gesamt", "всего", "合計", "totale", "total", "łącznie", "totaal", "toplam", "합계", "总计", "усього", "totalt", "celkem");
    private static Action<int, int>? _onMoved;
    private static Action<bool>? _onLockChanged;
    private static volatile bool _locked;
    private static bool _offscreen;
    private static int _stableW;
    private static RECT _lockRect; // lock button, client coordinates
    private static IntPtr _fontIcon;
    private static WndProcDelegate? _wndProc; // keep the delegate alive
    private static IntPtr _fontBig, _fontSmall, _brushBg, _brushPaintBg;
    private static volatile OverlayTheme _theme = OverlayTheme.Default;
    private static float _scale = 1f;    // display scaling × size chosen by the player
    private static float _dpiScale = 1f; // display scaling only
    private static volatile int _sizePercent = 100;
    private static volatile int _opacityPercent = 100;

    private const int MinWidth = 240, Pad = 16, ColGap = 36;

    /// <summary>Size and opacity of the overlay (percent). Applied at once when the overlay is shown.</summary>
    public static void SetAppearance(int sizePercent, int opacityPercent)
    {
        _sizePercent = OverlayAppearance.ClampSize(sizePercent);
        _opacityPercent = OverlayAppearance.ClampOpacity(opacityPercent);
        if (_hwnd != IntPtr.Zero)
        {
            PostMessageW(_hwnd, WM_APP_APPEARANCE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static byte Alpha => (byte)Math.Round(_opacityPercent * 255 / 100.0);

    /// <summary>Colours of the overlay (a theme add-on, or the default look when null). Applied at once when running.</summary>
    public static void SetTheme(OverlayTheme? theme)
    {
        _theme = theme ?? OverlayTheme.Default;
        if (_hwnd != IntPtr.Zero)
        {
            PostMessageW(_hwnd, WM_APP_APPEARANCE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public static bool IsRunning => _hwnd != IntPtr.Zero;

    /// <summary>True when the global Ctrl+Shift+O shortcut could be registered.</summary>
    public static bool HotkeyRegistered { get; private set; }

    /// <summary>True when the global Ctrl+Shift+L shortcut (lock/unlock) could be registered.</summary>
    public static bool LockHotkeyRegistered { get; private set; }

    /// <summary>Locked = click-through: the mouse goes through the overlay and it cannot be moved.</summary>
    public static bool IsLocked => _locked;

    public static void Start(int? x, int? y, bool locked, Action<int, int> onMoved, Action<bool> onLockChanged)
    {
        if (_thread != null)
        {
            return;
        }

        _onMoved = onMoved;
        _offscreen = x <= -10000; // --overlay-preview: never brought back on screen
        _onLockChanged = onLockChanged;
        _locked = locked;
        var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            try
            {
                Run(x, y, ready);
            }
            catch (Exception ex)
            {
                Console.WriteLine(L.T($"Overlay indisponible : {ex.Message}", $"Overlay unavailable: {ex.Message}", $"Overlay no disponible: {ex.Message}", $"Overlay nicht verfügbar: {ex.Message}", $"Оверлей недоступен: {ex.Message}", $"オーバーレイを利用できません: {ex.Message}", $"Overlay non disponibile: {ex.Message}", $"Sobreposição indisponível: {ex.Message}", $"Nakładka niedostępna: {ex.Message}", $"Overlay niet beschikbaar: {ex.Message}", $"Kaplama kullanılamıyor: {ex.Message}", $"오버레이 사용 불가: {ex.Message}", $"叠加层不可用：{ex.Message}", $"Оверлей недоступний: {ex.Message}", $"Överlägget är inte tillgängligt: {ex.Message}", $"Překryv není k dispozici: {ex.Message}"));
                ready.Set();
            }
        })
        {
            IsBackground = true,
            Name = "Overlay",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(5));
    }

    /// <summary>Replaces what is shown. The window resizes itself to fit.</summary>
    public static void Update(OverlayView view)
    {
        _view = view;
        if (_hwnd != IntPtr.Zero)
        {
            PostMessageW(_hwnd, WM_APP_RELAYOUT, IntPtr.Zero, IntPtr.Zero);
        }
    }

    /// <summary>Saves the overlay as it is drawn to a PNG file (works off-screen). Used by --overlay-preview.</summary>
    public static bool Snapshot(string path)
    {
        if (_hwnd == IntPtr.Zero || !GetWindowRect(_hwnd, out var r))
        {
            return false;
        }
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        var screen = GetDC(IntPtr.Zero);
        var mem = CreateCompatibleDC(screen);
        var bmp = CreateCompatibleBitmap(screen, w, h);
        var old = SelectObject(mem, bmp);
        var ok = PrintWindow(_hwnd, mem, 1 /* PW_CLIENTONLY: WM_PRINT -> WM_PRINTCLIENT */);
        SelectObject(mem, old);
        var bi = new BITMAPINFOHEADER { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
        var bgra = new byte[w * h * 4];
        GetDIBits(mem, bmp, 0, (uint)h, bgra, ref bi, 0);
        DeleteObject(bmp);
        DeleteDC(mem);
        ReleaseDC(IntPtr.Zero, screen);
        Png.Write(path, w, h, bgra);
        return ok;
    }

    public static void ToggleLock()
    {
        if (_hwnd != IntPtr.Zero)
        {
            PostMessageW(_hwnd, WM_APP_LOCK, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public static void Toggle()
    {
        if (_hwnd != IntPtr.Zero)
        {
            PostMessageW(_hwnd, WM_APP_TOGGLE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static void Run(int? x, int? y, ManualResetEventSlim ready)
    {
        try { SetProcessDPIAware(); } catch { /* old Windows */ }

        var screenDc = GetDC(IntPtr.Zero);
        _dpiScale = GetDeviceCaps(screenDc, LOGPIXELSY) / 96f;
        ReleaseDC(IntPtr.Zero, screenDc);

        int S(int v) => (int)Math.Round(v * _scale);

        CreateFonts();
        _brushBg = CreateSolidBrush(OverlayTheme.Default.Background);
        _brushPaintBg = CreateSolidBrush(_theme.Background);
        _brushBorder = CreateSolidBrush(_theme.Border);
        _brushLine = CreateSolidBrush(_theme.Line);

        var hInstance = GetModuleHandleW(null);
        _wndProc = WndProc;
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            hCursor = LoadCursorW(IntPtr.Zero, IDC_SIZEALL),
            hbrBackground = _brushBg,
            lpszClassName = "ChestCounterOverlay",
        };
        if (RegisterClassExW(ref wc) == 0)
        {
            throw new InvalidOperationException("RegisterClassEx failed");
        }

        int w = S(MinWidth), h = S(90);
        var posX = x ?? S(24);
        var posY = y ?? S(24);

        _hwnd = CreateWindowExW(
            // Opaque (no WS_EX_LAYERED alpha): nothing for the compositor to blend every frame.
            WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
            wc.lpszClassName, L.T("Coffres", "Chests", "Cofres", "Truhen", "Сундуки", "宝箱", "Forzieri", "Baús", "Skrzynie", "Kisten", "Sandıklar", "상자", "宝箱", "Скрині", "Kistor", "Truhly"), WS_POPUP,

            posX, posY, w, h, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("CreateWindowEx failed");
        }

        ApplyLock(_hwnd, _locked, notify: false);
        Relayout(_hwnd);
        ShowWindow(_hwnd, SW_SHOWNOACTIVATE);
        // No timer at all: the thread sleeps in GetMessage and only wakes up when the counter changes,
        // when you drag/hide the window or press the shortcut.
        // Global shortcut, works even when the game has the focus.
        HotkeyRegistered = RegisterHotKey(_hwnd, 1, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_O);
        LockHotkeyRegistered = RegisterHotKey(_hwnd, 2, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_L);
        ready.Set();

        while (GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
    }

    /// <summary>(Re)creates the fonts for the current display scaling and chosen size.</summary>
    private static void CreateFonts()
    {
        foreach (var old in new[] { _fontBig, _fontSmall, _fontHeader, _fontCaption, _fontLabel, _fontIcon })
        {
            if (old != IntPtr.Zero) DeleteObject(old);
        }
        _scale = _dpiScale * _sizePercent / 100f;
        int S(int v) => (int)Math.Round(v * _scale);
        _fontBig = CreateFontW(-S(38), 0, 0, 0, 800, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        _fontSmall = CreateFontW(-S(14), 0, 0, 0, 500, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        _fontHeader = CreateFontW(-S(12), 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        _fontCaption = CreateFontW(-S(11), 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        _fontLabel = CreateFontW(-S(13), 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        _fontIcon = CreateFontW(-S(14), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe MDL2 Assets");
    }

    private static IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
            {
                // Lock button = normal client area (clickable); everywhere else = caption (drag to move).
                // When locked the window is WS_EX_TRANSPARENT, so it never even receives this message.
                GetWindowRect(hwnd, out var wr);
                var px = (short)((long)lParam & 0xFFFF) - wr.Left;
                var py = (short)(((long)lParam >> 16) & 0xFFFF) - wr.Top;
                var onButton = px >= _lockRect.Left && px < _lockRect.Right && py >= _lockRect.Top && py < _lockRect.Bottom;
                return (IntPtr)(onButton ? HTCLIENT : HTCAPTION);
            }

            case WM_SETCURSOR:
                if (((long)lParam & 0xFFFF) == HTCLIENT)
                {
                    SetCursor(LoadCursorW(IntPtr.Zero, IDC_HAND));
                    return (IntPtr)1;
                }
                break;

            case WM_LBUTTONUP:
                ApplyLock(hwnd, true, notify: true);
                return IntPtr.Zero;

            case WM_APP_LOCK:
                ApplyLock(hwnd, !_locked, notify: true);
                return IntPtr.Zero;

            case WM_NCRBUTTONUP:
                SetVisible(hwnd, false);
                return IntPtr.Zero;

            case WM_HOTKEY:
                if ((long)wParam == 2)
                {
                    ApplyLock(hwnd, !_locked, notify: true);
                }
                else
                {
                    SetVisible(hwnd, !IsWindowVisible(hwnd));
                }
                return IntPtr.Zero;

            case WM_EXITSIZEMOVE:
                if (GetWindowRect(hwnd, out var r))
                {
                    _onMoved?.Invoke(r.Left, r.Top);
                }
                return IntPtr.Zero;

            case WM_APP_TOGGLE:
                SetVisible(hwnd, !IsWindowVisible(hwnd));
                return IntPtr.Zero;

            case WM_APP_RELAYOUT:
                Relayout(hwnd);
                return IntPtr.Zero;

            case WM_APP_APPEARANCE:
                RebuildBrushes();
                CreateFonts();
                _stableW = 0; // the width may shrink when the overlay gets smaller
                ApplyLock(hwnd, _locked, notify: false); // also applies the opacity
                Relayout(hwnd);
                return IntPtr.Zero;

            case WM_PAINT:
                Paint(hwnd);
                return IntPtr.Zero;

            case WM_PRINTCLIENT: // snapshot (--overlay-preview): draw into the given DC
                PaintTo(hwnd, wParam);
                return IntPtr.Zero;

            case WM_DESTROY:
                UnregisterHotKey(hwnd, 1);
                UnregisterHotKey(hwnd, 2);
                PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Locked: click-through (WS_EX_LAYERED + WS_EX_TRANSPARENT). Unlocked: normal window. Layered also when the
    /// player made it see-through; a fully opaque unlocked overlay stays a plain window (nothing to blend).
    /// </summary>
    private static void ApplyLock(IntPtr hwnd, bool locked, bool notify)
    {
        _locked = locked;
        var alpha = Alpha;
        var layered = locked || alpha < 255;
        var ex = (long)GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
        ex = layered ? ex | WS_EX_LAYERED : ex & ~(long)WS_EX_LAYERED;
        ex = locked ? ex | WS_EX_TRANSPARENT : ex & ~(long)WS_EX_TRANSPARENT;
        SetWindowLongPtrW(hwnd, GWL_EXSTYLE, (IntPtr)ex);
        if (layered)
        {
            SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA); // required for a layered window to be drawn
        }
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        InvalidateRect(hwnd, IntPtr.Zero, true);

        if (notify)
        {
            _onLockChanged?.Invoke(locked);
            Console.WriteLine(locked
                ? L.T($"Overlay verrouillé : la souris passe au travers. {(LockHotkeyRegistered ? "Ctrl+Maj+L" : "Touche L dans cette fenêtre")} pour le déverrouiller.",
                      $"Overlay locked: the mouse goes through it. {(LockHotkeyRegistered ? "Ctrl+Shift+L" : "L key in this window")} to unlock it.",
                      $"Overlay bloqueado: el ratón lo atraviesa. {(LockHotkeyRegistered ? "Ctrl+Mayús+L" : "Tecla L en esta ventana")} para desbloquearlo.",
                      $"Overlay gesperrt: Die Maus geht hindurch. {(LockHotkeyRegistered ? "Strg+Umschalt+L" : "Taste L in diesem Fenster")} zum Entsperren.",
                      $"Оверлей заблокирован: мышь проходит сквозь него. {(LockHotkeyRegistered ? "Ctrl+Shift+L" : "Клавиша L в этом окне")} — чтобы разблокировать.",
                      $"オーバーレイをロックしました（マウスは透過します）。{(LockHotkeyRegistered ? "Ctrl+Shift+L" : "このウィンドウで L キー")} で解除します。",
                      $"Overlay bloccato: il mouse lo attraversa. {(LockHotkeyRegistered ? "Ctrl+Shift+L" : "Tasto L in questa finestra")} per sbloccarlo.",
                      $"Sobreposição travada: o mouse passa através dela. {(LockHotkeyRegistered ? "Ctrl+Shift+L" : "Tecla L nesta janela")} para destravar.",
                      $"Nakładka zablokowana: mysz przez nią przechodzi. {(LockHotkeyRegistered ? "Ctrl+Shift+L" : "Klawisz L w tym oknie")}, aby odblokować.",
                      $"Overlay vergrendeld: de muis gaat erdoorheen. {(LockHotkeyRegistered ? "Ctrl+Shift+L" : "L-toets in dit venster")} om te ontgrendelen.",
                      $"Kaplama kilitli: fare içinden geçer. Kilidi açmak için {(LockHotkeyRegistered ? "Ctrl+Shift+L" : "bu penceredeki L tuşu")}.",
                      $"오버레이 잠김: 마우스가 통과합니다. 잠금 해제: {(LockHotkeyRegistered ? "Ctrl+Shift+L" : "이 창에서 L 키")}.",
                      $"叠加层已锁定：鼠标可穿透。按 {(LockHotkeyRegistered ? "Ctrl+Shift+L" : "此窗口中的 L 键")} 解锁。",
                      $"Оверлей заблоковано: миша проходить крізь нього. {(LockHotkeyRegistered ? "Ctrl+Shift+L" : "Клавіша L у цьому вікні")} — розблокувати.",
                      $"Överlägget är låst: musen går igenom det. {(LockHotkeyRegistered ? "Ctrl+Shift+L" : "L-tangenten i det här fönstret")} för att låsa upp.",
                      $"Překryv zamčen: myš jím prochází. {(LockHotkeyRegistered ? "Ctrl+Shift+L" : "Klávesa L v tomto okně")} pro odemčení.")
                : L.T("Overlay déverrouillé : clic gauche maintenu pour le déplacer, cadenas pour le reverrouiller.",
                      "Overlay unlocked: hold the left mouse button to move it, click the padlock to lock it again.",
                      "Overlay desbloqueado: arrástralo con el clic izquierdo; el candado lo vuelve a bloquear.",
                      "Overlay entsperrt: Linke Maustaste gedrückt halten zum Verschieben, Schloss anklicken zum Sperren.",
                      "Оверлей разблокирован: перетащите его левой кнопкой мыши, нажмите на замок, чтобы снова заблокировать.",
                      "オーバーレイのロックを解除しました。左ボタンを押したままで移動、鍵アイコンで再ロックします。",
                      "Overlay sbloccato: tieni premuto il tasto sinistro del mouse per spostarlo, clicca il lucchetto per bloccarlo di nuovo.",
                      "Sobreposição destravada: segure o botão esquerdo do mouse para movê-la, clique no cadeado para travá-la de novo.",
                      "Nakładka odblokowana: przytrzymaj lewy przycisk myszy, aby ją przesunąć, kliknij kłódkę, aby ją znów zablokować.",
                      "Overlay ontgrendeld: houd de linkermuisknop ingedrukt om hem te verplaatsen, klik op het hangslot om hem weer te vergrendelen.",
                      "Kaplamanın kilidi açık: taşımak için farenin sol tuşunu basılı tut, yeniden kilitlemek için asma kilide tıkla.",
                      "오버레이 잠금 해제됨: 마우스 왼쪽 버튼을 누른 채 이동하고, 자물쇠를 클릭하면 다시 잠깁니다.",
                      "叠加层已解锁：按住鼠标左键移动，点击锁图标重新锁定。",
                      "Оверлей розблоковано: утримуйте ліву кнопку миші, щоб перемістити його, клацніть замок, щоб знову заблокувати.",
                      "Överlägget är upplåst: håll ner vänster musknapp för att flytta det, klicka på hänglåset för att låsa det igen.",
                      "Překryv odemčen: drž levé tlačítko myši pro přesun, klikni na zámek pro opětovné zamčení."));
        }
    }

    private static void SetVisible(IntPtr hwnd, bool visible)
    {
        if (visible)
        {
            ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        else
        {
            ShowWindow(hwnd, SW_HIDE);
            Console.WriteLine(HotkeyRegistered
                ? L.T("Overlay masqué : Ctrl+Maj+O (n'importe où) pour le réafficher.", "Overlay hidden: Ctrl+Shift+O (anywhere) to show it again.", "Overlay oculto: Ctrl+Mayús+O (desde cualquier sitio) para volver a mostrarlo.", "Overlay ausgeblendet: Strg+Umschalt+O (überall) zum erneuten Anzeigen.", "Оверлей скрыт: Ctrl+Shift+O (где угодно), чтобы показать снова.", "オーバーレイを非表示にしました。Ctrl+Shift+O（どこでも）で再表示します。", "Overlay nascosto: Ctrl+Shift+O (ovunque) per mostrarlo di nuovo.", "Sobreposição oculta: Ctrl+Shift+O (em qualquer lugar) para mostrá-la de novo.", "Nakładka ukryta: Ctrl+Shift+O (w dowolnym miejscu), aby ją znów pokazać.", "Overlay verborgen: Ctrl+Shift+O (overal) om hem weer te tonen.", "Kaplama gizli: yeniden göstermek için Ctrl+Shift+O (her yerde).", "오버레이 숨김: 다시 표시하려면 Ctrl+Shift+O (어디서나).", "叠加层已隐藏：按 Ctrl+Shift+O（任意位置）重新显示。", "Оверлей сховано: Ctrl+Shift+O (будь-де), щоб показати знову.", "Överlägget är dolt: Ctrl+Shift+O (var som helst) för att visa det igen.", "Překryv skryt: Ctrl+Shift+O (kdekoli) pro opětovné zobrazení.")
                : L.T("Overlay masqué : touche O dans cette fenêtre pour le réafficher.", "Overlay hidden: O key in this window to show it again.", "Overlay oculto: tecla O en esta ventana para volver a mostrarlo.", "Overlay ausgeblendet: Taste O in diesem Fenster zum erneuten Anzeigen.", "Оверлей скрыт: клавиша O в этом окне, чтобы показать снова.", "オーバーレイを非表示にしました。このウィンドウで O キーを押すと再表示します。", "Overlay nascosto: tasto O in questa finestra per mostrarlo di nuovo.", "Sobreposição oculta: tecla O nesta janela para mostrá-la de novo.", "Nakładka ukryta: klawisz O w tym oknie, aby ją znów pokazać.", "Overlay verborgen: O-toets in dit venster om hem weer te tonen.", "Kaplama gizli: yeniden göstermek için bu pencerede O tuşu.", "오버레이 숨김: 다시 표시하려면 이 창에서 O 키.", "叠加层已隐藏：在此窗口按 O 键重新显示。", "Оверлей сховано: клавіша O у цьому вікні, щоб показати знову.", "Överlägget är dolt: O-tangenten i det här fönstret för att visa det igen.", "Překryv skryt: klávesa O v tomto okně pro opětovné zobrazení."));
        }
    }

    private static int TextWidth(IntPtr hdc, IntPtr font, string text, out int height)
    {
        SelectObject(hdc, font);
        var r = new RECT();
        DrawTextW(hdc, text.Length == 0 ? " " : text, -1, ref r, DT_CALCRECT | DT_SINGLELINE | DT_NOPREFIX);
        height = r.Bottom - r.Top;
        return r.Right - r.Left;
    }

    /// <summary>Measures the text and resizes the window (and its rounded region) to fit it.</summary>
    private static void Relayout(IntPtr hwnd)
    {
        int S(int v) => (int)Math.Round(v * _scale);
        var v = _view;
        var hdc = GetDC(hwnd);
        try
        {
            // Header: "COFFRES" ... "● EN DIRECT" [lock]
            var headerW = TextWidth(hdc, _fontHeader, v.HeaderText ?? Header, out _headerH) + S(16) + (v.Badge != null ? TextWidth(hdc, _fontHeader, v.Badge, out _) : 0) + S(30);

            // Two counters side by side.
            _col1W = Math.Max(TextWidth(hdc, _fontBig, v.Total, out _bigH), TextWidth(hdc, _fontCaption, v.TotalCaption ?? CaptionTotal, out _captionH));
            _col2W = Math.Max(TextWidth(hdc, _fontBig, v.Current ?? "—", out _), Math.Max(TextWidth(hdc, _fontCaption, v.CurrentCaption, out _), TextWidth(hdc, _fontCaption, OverlayModel.LastMatchCaption, out _)));
            var countersW = _col1W + S(ColGap) + _col2W;

            // Detail rows: grey label column + value.
            _labelW = 0;
            var valueW = 0;
            _rowH = 0;
            foreach (var row in v.Rows)
            {
                _labelW = Math.Max(_labelW, TextWidth(hdc, _fontLabel, row.Label, out var lh));
                valueW = Math.Max(valueW, TextWidth(hdc, _fontSmall, row.Value, out var vh)
                    + (row.Detail != null ? S(6) + TextWidth(hdc, _fontCaption, row.Detail, out _) : 0));
                _rowH = Math.Max(_rowH, Math.Max(lh, vh));
            }
            var rowsW = v.Rows.Count > 0 ? _labelW + S(12) + valueW : 0;

            // Never shrinks: the overlay keeps the same size between a match in progress and the end of the match.
            var w = Math.Max(_stableW, Math.Max(S(MinWidth), Math.Max(headerW, Math.Max(countersW, rowsW)) + 2 * S(Pad)));
            _stableW = w;
            var h = S(12) + _headerH + S(6) + _bigH + _captionH + S(12);
            if (v.Rows.Count > 0)
            {
                h += 1 + S(10) + v.Rows.Count * (_rowH + S(4)) + S(4);
            }
            _lockRect = new RECT { Left = w - S(Pad) - S(18), Top = S(6), Right = w - S(6), Bottom = S(12) + _headerH + S(6) };
            // Keep it fully on its screen: on the right half of the screen it grows to the left, and it is
            // pushed back inside when it would overflow (a wider overlay used to be cut by the screen edge).
            GetWindowRect(hwnd, out var cur);
            int nx = cur.Left, ny = cur.Top;
            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (!_offscreen && GetMonitorInfoW(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref mi))
            {
                var work = mi.rcWork;
                if (cur.Right - cur.Left > 0 && (cur.Left + cur.Right) / 2 > (work.Left + work.Right) / 2)
                {
                    nx = cur.Right - w; // anchored on the right edge
                }
                nx = Math.Max(work.Left, Math.Min(nx, work.Right - w));
                ny = Math.Max(work.Top, Math.Min(ny, work.Bottom - h));
            }
            SetWindowPos(hwnd, HWND_TOPMOST, nx, ny, w, h, SWP_NOACTIVATE);
            SetWindowRgn(hwnd, CreateRoundRectRgn(0, 0, w + 1, h + 1, S(18), S(18)), true); // window owns the region
            if (nx != cur.Left || ny != cur.Top)
            {
                _onMoved?.Invoke(nx, ny);
            }
        }
        finally
        {
            ReleaseDC(hwnd, hdc);
        }
        InvalidateRect(hwnd, IntPtr.Zero, true);
        UpdateWindow(hwnd); // paint now (one repaint per update, no timer)
    }

    private static void Text(IntPtr hdc, IntPtr font, uint color, string text, int left, int top, int right, int bottom, uint align = DT_LEFT)
    {
        SelectObject(hdc, font);
        SetTextColor(hdc, color);
        var r = new RECT { Left = left, Top = top, Right = right, Bottom = bottom };
        DrawTextW(hdc, text, -1, ref r, DT_SINGLELINE | DT_NOPREFIX | align);
    }

    private static void Paint(IntPtr hwnd)
    {
        var hdc = BeginPaint(hwnd, out var ps);
        try
        {
            PaintTo(hwnd, hdc);
        }
        finally
        {
            EndPaint(hwnd, ref ps);
        }
    }

    private static void PaintTo(IntPtr hwnd, IntPtr hdc)
    {
        var v = _view;
        {
            GetClientRect(hwnd, out var rc);
            FillRect(hdc, ref rc, _brushPaintBg);
            SetBkMode(hdc, TRANSPARENT);

            int S(int n) => (int)Math.Round(n * _scale);
            var x = S(Pad);
            var right = rc.Right - S(Pad);
            var y = S(12);

            // Thin border following the rounded shape.
            var border = CreateRoundRectRgn(0, 0, rc.Right + 1, rc.Bottom + 1, S(18), S(18));
            FrameRgn(hdc, border, _brushBorder, 1, 1);
            DeleteObject(border);

            // Header
            Text(hdc, _fontHeader, _theme.Muted, v.HeaderText ?? Header, x, y, right, y + _headerH);
            if (v.Badge != null)
            {
                Text(hdc, _fontHeader, _theme.Accent, v.Badge, x, y, _lockRect.Left - S(6), y + _headerH, DT_RIGHT);
            }
            // Lock button (Segoe MDL2 Assets: E785 = open padlock, E72E = closed padlock).
            SelectObject(hdc, _fontIcon);
            SetTextColor(hdc, _locked ? Rgb(71, 85, 105) : Rgb(203, 213, 225));
            var lr = _lockRect;
            DrawTextW(hdc, _locked ? "" : "", -1, ref lr, DT_SINGLELINE | DT_CENTER | DT_VCENTER | DT_NOPREFIX);
            y += _headerH + S(6);

            // Counters: total | match in progress
            var x2 = x + _col1W + S(ColGap);
            Text(hdc, _fontBig, _theme.Accent, v.Total, x - S(1), y, x + _col1W + S(4), y + _bigH);
            Text(hdc, _fontBig, v.Current == null ? Rgb(71, 85, 105) : v.CurrentLive ? _theme.Accent : _theme.Text, v.Current ?? "—", x2 - S(1), y, right + S(4), y + _bigH);
            var sep = new RECT { Left = x + _col1W + S(ColGap) / 2, Top = y + S(8), Right = x + _col1W + S(ColGap) / 2 + 1, Bottom = y + _bigH + _captionH };
            FillRect(hdc, ref sep, _brushLine);
            y += _bigH;
            Text(hdc, _fontCaption, _theme.Muted, v.TotalCaption ?? CaptionTotal, x, y, x + _col1W + S(4), y + _captionH);
            Text(hdc, _fontCaption, _theme.Muted, v.CurrentCaption, x2, y, right + S(4), y + _captionH);
            y += _captionH + S(12);

            if (v.Rows.Count > 0)
            {
                var line = new RECT { Left = x, Top = y, Right = right, Bottom = y + 1 };
                FillRect(hdc, ref line, _brushLine);
                y += 1 + S(10);
                foreach (var row in v.Rows)
                {
                    Text(hdc, _fontLabel, _theme.Muted, row.Label, x, y, x + _labelW, y + _rowH);
                    var vx = x + _labelW + S(12);
                    Text(hdc, _fontSmall, row.Color != null ? OverlayTheme.FromHex(row.Color) : row.Treasure ? _theme.Treasure : _theme.Text, row.Value, vx, y, right + S(4), y + _rowH);
                    if (row.Detail != null)
                    {
                        var dx = vx + TextWidth(hdc, _fontSmall, row.Value, out _) + S(6);
                        Text(hdc, _fontCaption, _theme.Muted, row.Detail, dx, y, right + S(4), y + _rowH - S(2), DT_BOTTOM);
                    }
                    y += _rowH + S(4);
                }
            }
        }
    }

    private static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));

    /// <summary>New brushes for the current theme (overlay thread). The window class brush is left alone.</summary>
    private static void RebuildBrushes()
    {
        foreach (var old in new[] { _brushPaintBg, _brushBorder, _brushLine })
        {
            if (old != IntPtr.Zero) DeleteObject(old);
        }
        var t = _theme;
        _brushPaintBg = CreateSolidBrush(t.Background);
        _brushBorder = CreateSolidBrush(t.Border);
        _brushLine = CreateSolidBrush(t.Line);
    }

    // ---- Win32 ------------------------------------------------------------------------------
    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WS_POPUP = 0x80000000;
    private const uint WS_EX_TOPMOST = 0x00000008, WS_EX_TRANSPARENT = 0x00000020, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_LAYERED = 0x00080000, WS_EX_NOACTIVATE = 0x08000000;
    private const int GWL_EXSTYLE = -20, HTCLIENT = 1;
    private const uint WM_SETCURSOR = 0x0020, WM_LBUTTONUP = 0x0202, WM_APP_LOCK = 0x8003, VK_L = 0x4C, SWP_FRAMECHANGED = 0x20;
    private static readonly IntPtr IDC_HAND = new(32649);
    private const uint LWA_ALPHA = 0x2;
    private const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4;
    private const uint WM_DESTROY = 0x0002, WM_PAINT = 0x000F, WM_NCHITTEST = 0x0084, WM_NCRBUTTONUP = 0x00A5,
        WM_TIMER = 0x0113, WM_EXITSIZEMOVE = 0x0232, WM_APP_TOGGLE = 0x8001, WM_PRINTCLIENT = 0x0318, WM_APP_RELAYOUT = 0x8002, WM_HOTKEY = 0x0312, WM_APP_APPEARANCE = 0x8004;
    private const uint MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000, VK_O = 0x4F;
    private const int HTCAPTION = 2;
    private const int TRANSPARENT = 1, LOGPIXELSY = 90;
    private const uint DT_LEFT = 0x0, DT_CENTER = 0x1, DT_RIGHT = 0x2, DT_VCENTER = 0x4, DT_BOTTOM = 0x8, DT_SINGLELINE = 0x20, DT_CALCRECT = 0x400, DT_NOPREFIX = 0x800;
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr IDC_SIZEALL = new(32646);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize, style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam, lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public int fErase;
        public RECT rcPaint;
        public int fRestore, fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetMessageW(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DispatchMessageW(ref MSG msg);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd, IntPtr rgn, bool redraw);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool InvalidateRect(IntPtr hwnd, IntPtr rect, bool erase);
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hwnd, ref PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern int FillRect(IntPtr hdc, ref RECT rect, IntPtr brush);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DrawTextW(IntPtr hdc, string text, int count, ref RECT rect, uint format);
    [DllImport("user32.dll")] private static extern UIntPtr SetTimer(IntPtr hwnd, UIntPtr id, uint ms, IntPtr proc);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursorW(IntPtr instance, IntPtr name);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtrW(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern IntPtr SetCursor(IntPtr cursor);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string? name);
    [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int index);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string face);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public uint cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfoW(IntPtr monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr bmp, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER bi, uint usage);
    [DllImport("gdi32.dll")] private static extern bool FrameRgn(IntPtr hdc, IntPtr rgn, IntPtr brush, int w, int h);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(IntPtr hdc, uint color);
}

/// <summary>Colours of the overlay as Win32 COLORREF values (0x00BBGGRR).</summary>
public sealed record OverlayTheme(uint Background, uint Border, uint Line, uint Muted, uint Text, uint Accent, uint Treasure)
{
    public static OverlayTheme Default { get; } = new(
        Colorref(9, 12, 18), Colorref(36, 41, 51), Colorref(26, 30, 39),
        Colorref(148, 163, 184), Colorref(248, 250, 252), Colorref(255, 200, 96), Colorref(196, 160, 255));

    public static uint Colorref(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));

    /// <summary>"#RRGGBB" to COLORREF; the caller has already validated the text.</summary>
    public static uint FromHex(string hex) =>
        Colorref(Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16));
}
