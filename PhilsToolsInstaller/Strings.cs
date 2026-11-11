using System.Collections.Generic;
using System.Globalization;

namespace PhilsTools
{
    /// <summary>The installer's texts in English, Spanish and French (index 0, 1, 2).</summary>
    public static class Strings
    {
        public const string ToolVersion = "1.6";
        public static int Lang;

        static readonly Dictionary<string, string[]> texts = new Dictionary<string, string[]>
        {
            { "Caption", new[] { "Install Phil's tools for Paratext", "Instalar las herramientas de Phil para Paratext", "Installer les outils de Phil pour Paratext" } },
            { "Title", new[] { "Phil's tools for Paratext", "Herramientas de Phil para Paratext", "Outils de Phil pour Paratext" } },
            { "Intro", new[] {
                "Tick the tools you want, then click Install. Tools already installed can be updated or removed the same way.",
                "Marque las herramientas que desea y haga clic en Instalar. Las herramientas ya instaladas se pueden actualizar o quitar de la misma manera.",
                "Cochez les outils voulus, puis cliquez sur Installer. Les outils d\u00e9j\u00e0 install\u00e9s peuvent \u00eatre mis \u00e0 jour ou supprim\u00e9s de la m\u00eame fa\u00e7on." } },
            { "CmsNote", new[] {
                "The CMS checks are now installed as Phils Custom (cms) Checks.",
                "Las verificaciones CMS ahora se instalan como Verificaciones personalizadas (cms) de Phil.",
                "Les v\u00E9rifications CMS s'installent maintenant comme V\u00E9rifications personnalis\u00E9es (cms) de Phil." } },
            { "Language", new[] { "Language:", "Idioma:", "Langue :" } },
            { "TabPlugins", new[] { "Paratext plugins", "Complementos de Paratext", "Modules compl\u00e9mentaires de Paratext" } },
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
            { "Guide", new[] { "User guide", "Gu\u00eda del usuario", "Guide de l'utilisateur" } },
            { "GuideError", new[] { "The user guide could not be opened: {0}", "No se pudo abrir la gu\u00eda del usuario: {0}", "Le guide de l'utilisateur n'a pas pu \u00eatre ouvert : {0}" } },
            { "OpenElsewhere", new[] { "Open in another program", "Abrir con otro programa", "Ouvrir avec un autre programme" } },
            { "CannotOpenElsewhere", new[] { "Windows could not open the file in another program: {0}", "Windows no pudo abrir el archivo con otro programa: {0}", "Windows n'a pas pu ouvrir le fichier avec un autre programme : {0}" } },
            { "Refresh", new[] { "Refresh", "Actualizar", "Actualiser" } },
            { "SelectAll", new[] { "Select all", "Marcar todo", "Tout cocher" } },
            { "SelectNone", new[] { "Select none", "Desmarcar todo", "Tout d\u00e9cocher" } },
            { "Install", new[] { "Install / update ticked", "Instalar / actualizar los marcados", "Installer / mettre \u00e0 jour" } },
            { "Remove", new[] { "Remove ticked", "Quitar los marcados", "Supprimer les coch\u00e9s" } },
            { "Close", new[] { "Close", "Cerrar", "Fermer" } },
            { "Menu", new[] { "Menu: Tools > {0}", "Men\u00fa: Herramientas > {0}", "Menu : Outils > {0}" } },
            { "NothingTicked", new[] { "Tick the tools you want first.", "Primero marque las herramientas que desea.", "Cochez d'abord les outils voulus." } },
            { "ConfirmRemovePlugins", new[] { "Remove {0} plugin(s)?", "\u00bfQuitar {0} complemento(s)?", "Supprimer {0} module(s) ?" } },
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
            { "BackedUp", new[] { "  {0} had been changed by hand: it was kept as {1}", "  {0} hab\u00eda sido modificado a mano: se guard\u00f3 como {1}", "  {0} avait \u00e9t\u00e9 modifi\u00e9 \u00e0 la main : il a \u00e9t\u00e9 gard\u00e9 sous le nom {1}" } },
            { "Deleted", new[] { "  Removed {0}", "  Quitado {0}", "  Supprim\u00e9 {0}" } },
            { "Kept", new[] { "  Kept {0}: {1} still uses it", "  Se conserv\u00f3 {0}: {1} todav\u00eda lo usa", "  {0} conserv\u00e9 : {1} l'utilise encore" } },
            { "FileError", new[] { "  Could not change {0}: {1}", "  No se pudo cambiar {0}: {1}", "  Impossible de modifier {0} : {1}" } },
            { "OldCopies", new[] {
                "{0} CMS check(s) from an earlier install are still in {1}. They now come with the Phils Custom (cms) Checks plugin.",
                "{0} verificaci\u00f3n(es) CMS de una instalaci\u00f3n anterior siguen en {1}. Ahora vienen con el complemento Verificaciones personalizadas (cms) de Phil.",
                "{0} v\u00e9rification(s) CMS d'une installation pr\u00e9c\u00e9dente sont encore dans {1}. Elles viennent maintenant avec le module V\u00e9rifications personnalis\u00e9es (cms) de Phil." } },
            { "OldRemove", new[] { "Remove old CMS copies...", "Quitar las copias CMS antiguas...", "Supprimer les anciennes copies CMS..." } },
            { "OldConfirm", new[] {
                "The checks now come with the Phils Custom (cms) Checks plugin.\n\nRemove the {0} old copies from:\n{1}\n\nFiles you changed by hand are kept as .bak copies, and other tools in the cms folder are not touched.",
                "Las verificaciones vienen ahora con el complemento Verificaciones personalizadas (cms) de Phil.\n\n\u00bfQuitar las {0} copias antiguas de:\n{1}?\n\nLos archivos que usted cambi\u00f3 a mano se guardan como copias .bak, y las dem\u00e1s herramientas de la carpeta cms no se tocan.",
                "Les v\u00e9rifications viennent maintenant avec le module V\u00e9rifications personnalis\u00e9es (cms) de Phil.\n\nSupprimer les {0} anciennes copies de :\n{1} ?\n\nLes fichiers que vous avez modifi\u00e9s \u00e0 la main sont gard\u00e9s comme copies .bak, et les autres outils du dossier cms ne sont pas touch\u00e9s." } },
            { "OldRemoving", new[] { "Removing old CMS copies from {0}", "Quitando las copias CMS antiguas de {0}", "Suppression des anciennes copies CMS de {0}" } },
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
