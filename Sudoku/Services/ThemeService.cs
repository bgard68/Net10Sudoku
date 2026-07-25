namespace Sudoku.Services;

public class ThemeService
{
    public event Action? OnThemeChanged;
    
    private bool _isDarkMode = false;
    
    public bool IsDarkMode
    {
        get => _isDarkMode;
        private set
        {
            if (_isDarkMode != value)
            {
                _isDarkMode = value;
                OnThemeChanged?.Invoke();
            }
        }
    }
    
    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
    }

    // Adopt a persisted preference (no-op when it already matches).
    public void SetDarkMode(bool darkMode)
    {
        IsDarkMode = darkMode;
    }
}
