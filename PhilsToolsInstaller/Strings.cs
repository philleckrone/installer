using System.Collections.Generic;
using System.Globalization;

namespace PhilsTools
{
    /// <summary>The installer's texts in English, Spanish and French (index 0, 1, 2).</summary>
    public static class Strings
    {
        public const string ToolVersion = "1.2";
        public static int Lang;

        static readonly Dictionary<string, string[]> texts = new Dictionary<string, string[]>
        {
            { "Caption", new[] { "Install Phil's tools for Paratext", "Instalar las herramientas de Phil para Paratext", "Installer les outils de Phil pour Paratext" } },
            { "Title", new[] { "Phil's tools for Paratext", "Herramientas de Phil para Paratext", "Outils de Phil pour Paratext" } },
            { "Intro", new[] {
                "Tick the tools you want, then click Install. Tools already installed can be updated or removed the same way.",
                "Marque las herramientas que desea y haga clic en Instalar. Las herramientas ya instaladas se pueden actualizar o quitar de la misma manera.",
                "Cochez les outils voulus, puis cliquez sur Installer. Les outils d\u00e9j\u00e0 install\u00e9s peuvent \u00eatre mis \u00e0 jour ou supprim\u00e9s de la m\u00eame fa\u00e7on." } },
            { "Language", new[] { "Language:", "Idioma:", "Langue :" } },
            { "TabPlugins", new[] { "Paratext plugins", "Complementos de Paratext", "Modules compl\u00e9mentaires de Paratext" } },
            { "TabCms", new[] { "CMS custom tools", "Herramientas personalizadas (CMS)", "Outils personnalis\u00e9s (CMS)" } },
            { "ColName", new[] { "Name", "Nombre", "Nom" } },
            { "ColVersion", new[] { "Version", "Versi\u00f3n", "Version" } },
            { "ColStatus", new[] { "Status", "Estado", "\u00c9tat" } },
            { "StNotInstalled", new[] { "Not installed", "No instalado", "Non install\u00e9" } },
            { "StInstalled", new[] { "Installed", "Instalado", "Install\u00e9" } },
            { "StUpdate", new[] { "Update available", "Actualizaci\u00f3n disponible", "Mise \u00e0 jour disponible" } },
            { "StNoParatext", new[] { "Paratext 9 not found", "No se encontr\u00f3 Paratext 9", "Paratext 9 introuvable" } },
            { "LangEn", new[] { "English", "English", "English" } },
            { "LangEs", new[] { "Espa\u00f1ol", "Espa\u00f1ol", "Espa\u00f1ol" } },
            { "ParatextFound", new[] { "Paratext 9 found: {0}", "Paratext 9 encontrado: {0}", "Paratext 9 trouv\u00e9 : {0}" } },
            { "ParatextNone", new[] {
                "No Paratext 9 installation was found on this computer, so plugins cannot be installed.",
                "No se encontr\u00f3 ninguna instalaci\u00f3n de Paratext 9 en esta computadora, as\u00ed que no se pueden instalar complementos.",
                "Aucune installation de Paratext 9 n'a \u00e9t\u00e9 trouv\u00e9e sur cet ordinateur ; les modules ne peuvent donc pas \u00eatre install\u00e9s." } },
            { "Running", new[] {
                "Paratext is open. Close Paratext before installing or removing plugins, then click Refresh.",
                "Paratext est\u00e1 abierto. Cierre Paratext antes de instalar o quitar complementos y haga clic en Actualizar.",
                "Paratext est ouvert. Fermez Paratext avant d'installer ou de supprimer des modules, puis cliquez sur Actualiser." } },
            { "Refresh", new[] { "Refresh", "Actualizar", "Actualiser" } },
            { "CmsFolder", new[] { "cms folder:", "Carpeta cms:", "Dossier cms :" } },
            { "Browse", new[] { "Browse...", "Examinar...", "Parcourir..." } },
            { "BrowseTitle", new[] {
                "Choose the cms folder inside your Paratext projects folder (for example C:\\My Paratext 9 Projects\\cms).",
                "Elija la carpeta cms dentro de la carpeta de proyectos de Paratext (por ejemplo C:\\My Paratext 9 Projects\\cms).",
                "Choisissez le dossier cms dans le dossier des projets Paratext (par exemple C:\\My Paratext 9 Projects\\cms)." } },
            { "CmsLang", new[] { "Install the CMS tools in:", "Instalar las herramientas CMS en:", "Installer les outils CMS en :" } },
            { "SelectAll", new[] { "Select all", "Marcar todo", "Tout cocher" } },
            { "SelectNone", new[] { "Select none", "Desmarcar todo", "Tout d\u00e9cocher" } },
            { "Install", new[] { "Install / update ticked", "Instalar / actualizar los marcados", "Installer / mettre \u00e0 jour" } },
            { "Remove", new[] { "Remove ticked", "Quitar los marcados", "Supprimer les coch\u00e9s" } },
            { "Close", new[] { "Close", "Cerrar", "Fermer" } },
            { "Menu", new[] { "Menu: Tools > {0}", "Men\u00fa: Herramientas > {0}", "Menu : Outils > {0}" } },
            { "NothingTicked", new[] { "Tick the tools you want first.", "Primero marque las herramientas que desea.", "Cochez d'abord les outils voulus." } },
            { "ConfirmRemovePlugins", new[] { "Remove {0} plugin(s)?", "\u00bfQuitar {0} complemento(s)?", "Supprimer {0} module(s) ?" } },
            { "ConfirmRemoveCms", new[] { "Remove {0} CMS tool(s)?", "\u00bfQuitar {0} herramienta(s) CMS?", "Supprimer {0} outil(s) CMS ?" } },
            { "PluginsSkipped", new[] {
                "Paratext is open, so no plugin was changed. Close Paratext and try again.",
                "Paratext est\u00e1 abierto, as\u00ed que no se cambi\u00f3 ning\u00fan complemento. Cierre Paratext e int\u00e9ntelo de nuevo.",
                "Paratext est ouvert ; aucun module n'a donc \u00e9t\u00e9 modifi\u00e9. Fermez Paratext et r\u00e9essayez." } },
            { "UacNote", new[] {
                "(Windows may ask for administrator permission, for folders that only administrators can change.)",
                "(Windows puede pedir permiso de administrador para las carpetas que solo los administradores pueden cambiar.)",
                "(Windows peut demander l'autorisation d'administrateur pour les dossiers que seuls les administrateurs peuvent modifier.)" } },
            { "Installing", new[] { "Installing {0}...", "Instalando {0}...", "Installation de {0}..." } },
            { "Removing", new[] { "Removing {0}...", "Quitando {0}...", "Suppression de {0}..." } },
            { "Done", new[] { "Finished.", "Terminado.", "Termin\u00e9." } },
            { "DoneErrors", new[] { "Finished with {0} problem(s): see the messages above.", "Terminado con {0} problema(s): vea los mensajes de arriba.", "Termin\u00e9 avec {0} probl\u00e8me(s) : voir les messages ci-dessus." } },
            { "RestartNote", new[] { "Start (or restart) Paratext to see the changes.", "Inicie (o reinicie) Paratext para ver los cambios.", "D\u00e9marrez (ou red\u00e9marrez) Paratext pour voir les changements." } },
            { "NoCmsDir", new[] { "Choose the cms folder first.", "Primero elija la carpeta cms.", "Choisissez d'abord le dossier cms." } },
            { "CmsNotExist", new[] { "The folder {0} does not exist.", "La carpeta {0} no existe.", "Le dossier {0} n'existe pas." } },
            { "AlsoInstalls", new[] { "Also installs: {0}", "Tambi\u00e9n instala: {0}", "Installe aussi : {0}" } },
            { "OnlyIn", new[] { "Available in: {0}", "Disponible en: {0}", "Disponible en : {0}" } },
            { "Wrote", new[] { "  Installed {0}", "  Instalado {0}", "  Install\u00e9 {0}" } },
            { "Unchanged", new[] { "  {0} is already up to date", "  {0} ya est\u00e1 actualizado", "  {0} est d\u00e9j\u00e0 \u00e0 jour" } },
            { "BackedUp", new[] { "  {0} had been changed by hand: it was kept as {1}", "  {0} hab\u00eda sido modificado a mano: se guard\u00f3 como {1}", "  {0} avait \u00e9t\u00e9 modifi\u00e9 \u00e0 la main : il a \u00e9t\u00e9 gard\u00e9 sous le nom {1}" } },
            { "Deleted", new[] { "  Removed {0}", "  Quitado {0}", "  Supprim\u00e9 {0}" } },
            { "Kept", new[] { "  Kept {0}: {1} still uses it", "  Se conserv\u00f3 {0}: {1} todav\u00eda lo usa", "  {0} conserv\u00e9 : {1} l'utilise encore" } },
            { "FileError", new[] { "  Could not change {0}: {1}", "  No se pudo cambiar {0}: {1}", "  Impossible de modifier {0} : {1}" } },
            { "LanguageNote", new[] { "  ({0} is only available in {1}.)", "  ({0} solo est\u00e1 disponible en {1}.)", "  ({0} n'est disponible qu'en {1}.)" } },
            { "ScriptFailed", new[] { "  {0}: the installer reported a problem (see above).", "  {0}: el instalador inform\u00f3 de un problema (vea arriba).", "  {0} : le programme d'installation a signal\u00e9 un probl\u00e8me (voir ci-dessus)." } },
        };

        public static string T(string key)
        {
            return texts[key][Lang];
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(texts[key][Lang], args);
        }

        public static string LangName(string code)
        {
            return code == "es" ? T("LangEs") : T("LangEn");
        }

        public static int FromCulture(string name)
        {
            if (string.IsNullOrEmpty(name)) name = CultureInfo.CurrentUICulture.Name;
            name = name.ToLowerInvariant();
            if (name.StartsWith("es")) return 1;
            if (name.StartsWith("fr")) return 2;
            return 0;
        }
    }
}
