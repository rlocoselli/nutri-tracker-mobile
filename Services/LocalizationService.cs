using Microsoft.Maui.Controls.Xaml;

namespace NutritionTracker.Services;

public static class LocalizationService
{
    private static readonly Dictionary<string, string> Fr = new()
    {
        ["tab_dashboard"] = "Tableau",
        ["tab_diary"] = "Journal",
        ["tab_add"] = "Ajouter",
        ["tab_goals"] = "Objectifs",
        ["tab_profile"] = "Profil",

        ["dashboard_title"] = "Tableau",
        ["hello"] = "Bonjour,",
        ["record_meal_plus"] = "Enregistrer un repas +",
        ["advice"] = "Conseils",
        ["goals"] = "Objectifs",
        ["daily_summary"] = "Résumé du jour",
        ["daily_summary_hint"] = "Ajoutez vos repas (texte ou photo) pour suivre calories, protéines et glucides.",
        ["steps"] = "Pas",
        ["burned_calories"] = "Calories dépensées",
        ["net_calories"] = "Net calorique: {0}",

        ["login_title"] = "Connexion",
        ["login_subtitle"] = "Suivez calories, protéines et glucides à partir d'un texte ou d'une photo.",
        ["login_google"] = "Continuer avec Google",
        ["login_disclaimer"] = "En continuant, vous acceptez d'envoyer vos données de repas au service d'analyse.",

        ["goals_title"] = "Objectifs",
        ["goal_cal_day"] = "Objectif calories / jour",
        ["goal_protein_day"] = "Objectif protéines / jour",
        ["goal_carbs_day"] = "Objectif glucides / jour",
        ["save"] = "Enregistrer",

        ["profile_title"] = "Profil",
        ["language"] = "Langue",
        ["current_lang_fr"] = "Langue actuelle : Français",
        ["current_lang_en"] = "Langue actuelle : Anglais",
        ["steps_today"] = "Pas aujourd'hui",
        ["advice_title"] = "Conseils",
        ["advice_hint"] = "Générez des recommandations basées sur votre historique.",
        ["generate_reco"] = "Générer des recommandations",
        ["logout"] = "Se déconnecter",

        ["main_loading"] = "Chargement...",

        ["recommendations_title"] = "Conseils",
        ["actions"] = "Actions :",

        ["sync_ok"] = "Google Fit synchronisé",
        ["sync_no_token"] = "Reconnectez-vous pour autoriser Google Fit",
        ["sync_error"] = "Sync Google Fit en échec",
        ["sync_disabled"] = "Intégration Google Fit temporairement désactivée",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["tab_dashboard"] = "Dashboard",
        ["tab_diary"] = "Diary",
        ["tab_add"] = "Add",
        ["tab_goals"] = "Goals",
        ["tab_profile"] = "Profile",

        ["dashboard_title"] = "Dashboard",
        ["hello"] = "Hello,",
        ["record_meal_plus"] = "Log meal +",
        ["advice"] = "Advice",
        ["goals"] = "Goals",
        ["daily_summary"] = "Daily summary",
        ["daily_summary_hint"] = "Add meals (text or photo) to track calories, protein and carbs.",
        ["steps"] = "Steps",
        ["burned_calories"] = "Burned calories",
        ["net_calories"] = "Net calories: {0}",

        ["login_title"] = "Login",
        ["login_subtitle"] = "Track calories, protein and carbs from text or photo.",
        ["login_google"] = "Continue with Google",
        ["login_disclaimer"] = "By continuing, you agree to send meal data to the analysis service.",

        ["goals_title"] = "Goals",
        ["goal_cal_day"] = "Calories target / day",
        ["goal_protein_day"] = "Protein target / day",
        ["goal_carbs_day"] = "Carbs target / day",
        ["save"] = "Save",

        ["profile_title"] = "Profile",
        ["language"] = "Language",
        ["current_lang_fr"] = "Current language: French",
        ["current_lang_en"] = "Current language: English",
        ["steps_today"] = "Today's steps",
        ["advice_title"] = "Advice",
        ["advice_hint"] = "Generate recommendations from your history.",
        ["generate_reco"] = "Generate recommendations",
        ["logout"] = "Sign out",

        ["main_loading"] = "Loading...",

        ["recommendations_title"] = "Advice",
        ["actions"] = "Actions:",

        ["sync_ok"] = "Google Fit synced",
        ["sync_no_token"] = "Sign in again to allow Google Fit",
        ["sync_error"] = "Google Fit sync failed",
        ["sync_disabled"] = "Google Fit integration temporarily disabled",
    };

    public static string T(string key)
    {
        var lang = Preferences.Default.Get("app_lang", "fr");
        var dict = lang == "en" ? En : Fr;
        return dict.TryGetValue(key, out var value) ? value : key;
    }
}

[ContentProperty(nameof(Key))]
public class TranslateExtension : IMarkupExtension<string>
{
    public string Key { get; set; } = "";

    public string ProvideValue(IServiceProvider serviceProvider)
        => LocalizationService.T(Key);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
        => ProvideValue(serviceProvider);
}
