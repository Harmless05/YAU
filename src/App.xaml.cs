using System.Windows;

namespace YAU
{
    public partial class App : Application
    {
        public enum Theme
        {
            Light,
            Dark
        }

        public static void ApplyTheme(Theme theme)
        {
            string themePath = theme == Theme.Light ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml";
            var themeDictionary = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };

            // Remove existing theme dictionaries
            var existingDictionaries = Current.Resources.MergedDictionaries;
            for (int i = existingDictionaries.Count - 1; i >= 0; i--)
            {
                var source = existingDictionaries[i].Source?.OriginalString;
                if (source == "Themes/LightTheme.xaml" || source == "Themes/DarkTheme.xaml")
                {
                    existingDictionaries.RemoveAt(i);
                }
            }

            // Add the new theme dictionary
            Current.Resources.MergedDictionaries.Add(themeDictionary);
        }
    }

}