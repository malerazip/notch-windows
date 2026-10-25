using System.Windows.Media;

namespace Notch.Sdk;

/// <summary>Cores e fontes do notch, para que plugins tenham o mesmo visual.</summary>
public static class NotchTheme
{
    public static readonly Brush Background = Solid(0x00, 0x00, 0x00);
    public static readonly Brush Surface = Solid(0x1C, 0x1C, 0x1E);
    public static readonly Brush SurfaceHover = Solid(0x2C, 0x2C, 0x2E);
    public static readonly Brush SurfaceStrong = Solid(0x3A, 0x3A, 0x3C);
    public static readonly Brush Text = Solid(0xFF, 0xFF, 0xFF);
    public static readonly Brush TextDim = Solid(0x8E, 0x8E, 0x93);
    public static readonly Brush TextFaint = Solid(0x48, 0x48, 0x4A);
    public static readonly Brush Accent = Solid(0x3E, 0xA6, 0xFF);
    public static readonly Brush AccentDeep = Solid(0x0B, 0x4F, 0x8C);
    public static readonly Brush Green = Solid(0x30, 0xD1, 0x58);
    public static readonly Brush Orange = Solid(0xFF, 0x9F, 0x0A);
    public static readonly Brush Red = Solid(0xFF, 0x45, 0x3A);
    public static readonly Brush Purple = Solid(0xBF, 0x5A, 0xF2);
    public static readonly Brush Yellow = Solid(0xFF, 0xD6, 0x0A);

    public static readonly FontFamily Font = new("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static SolidColorBrush Solid(byte r, byte g, byte b, byte a = 0xFF)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Glifos da fonte Segoe Fluent Icons usados no app.</summary>
public static class Glyphs
{
    public const string Info = "";
    public const string Play = "";
    public const string Pause = "";
    public const string Next = "";
    public const string Previous = "";
    public const string Music = "";
    public const string Lyrics = "";
    public const string Timer = "";
    public const string Drop = "";
    public const string Bell = "";
    public const string BellOff = "";
    public const string Settings = "";
    public const string Add = "";
    public const string Edit = "";
    public const string Delete = "";
    public const string Check = "";
    public const string Close = "";
    public const string ChevronLeft = "";
    public const string ChevronRight = "";
    public const string Folder = "";
    public const string Battery = "";
    public const string Charging = "";
    public const string Wifi = "";
    public const string WifiOff = "";
    public const string Bluetooth = "";
    public const string Volume = "";
    public const string Mute = "";
    public const string Microphone = "";
    public const string MicOff = "";
    public const string Brightness = "";
    public const string Keyboard = "";
    public const string CapsLock = "";
    public const string Globe = "";
    public const string Refresh = "";
    public const string Skip = "";
    public const string Reset = "";
    public const string Clock = "";
    public const string Megaphone = "";
    public const string Link = "";
    public const string Message = "";
    public const string Coffee = "";
    public const string History = "";
    public const string Gamepad = "";
    public const string Webcam = "";
}
