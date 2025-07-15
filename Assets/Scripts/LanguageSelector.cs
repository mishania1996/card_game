using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using TMPro;

public class LanguageSelector : MonoBehaviour
{
    public TMP_Dropdown dropdown;

    void Start()
    {
        // Clear any existing options from the dropdown.
        dropdown.ClearOptions();

        // Create a new list to hold the names of our languages.
        List<string> options = new List<string>();
        int currentLocaleIndex = 0;

        // Loop through all the languages you've set up in your project.
        for (int i = 0; i < LocalizationSettings.AvailableLocales.Locales.Count; i++)
        {
            var locale = LocalizationSettings.AvailableLocales.Locales[i];

            // Add the language name to our list.
            options.Add(locale.LocaleName);

            // Check if this locale is the one currently selected.
            if (LocalizationSettings.SelectedLocale == locale)
            {
                currentLocaleIndex = i;
            }
        }

        // Add the language names to the dropdown's options.
        dropdown.AddOptions(options);

        // Set the dropdown to show the currently selected language.
        dropdown.value = currentLocaleIndex;

        // Make sure the dropdown's OnValueChanged event is set up to call our method.
        dropdown.onValueChanged.AddListener(ChangeLanguage);
    }

    // This method is called by the dropdown when a new option is selected.
    public void ChangeLanguage(int localeIndex)
    {
        Debug.Log($"ChangeLanguage called with index: {localeIndex}");
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[localeIndex];
    }
}
