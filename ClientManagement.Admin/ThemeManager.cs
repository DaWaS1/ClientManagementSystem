namespace ClientManagement.Admin;

public static class ThemeManager
{
    // Az Admin felület szándékosan fix sötét témát használ.
    // A régi ThemeChanged eseményt kompatibilitás miatt megtartjuk,
    // de fix dark módban soha nem vált témát.
    public static event EventHandler? ThemeChanged
    {
        add { }
        remove { }
    }

    public static bool IsDarkMode => true;

    public static Color Background => Color.FromArgb(17, 24, 39);
    public static Color Surface => Color.FromArgb(31, 41, 55);
    public static Color SurfaceAlternative => Color.FromArgb(24, 33, 47);
    public static Color Sidebar => Color.FromArgb(11, 18, 32);
    public static Color SidebarHover => Color.FromArgb(30, 41, 59);
    public static Color TextPrimary => Color.FromArgb(243, 244, 246);
    public static Color TextSecondary => Color.FromArgb(156, 163, 175);
    public static Color Border => Color.FromArgb(55, 65, 81);
    public static Color InputBackground => Color.FromArgb(15, 23, 42);

    public static Color Primary => Color.FromArgb(37, 99, 235);
    public static Color Success => Color.FromArgb(22, 163, 74);
    public static Color Warning => Color.FromArgb(234, 88, 12);
    public static Color Danger => Color.FromArgb(220, 38, 38);
    public static Color Selection => Color.FromArgb(30, 64, 175);
}
