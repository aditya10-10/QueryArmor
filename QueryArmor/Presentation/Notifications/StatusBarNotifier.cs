using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace QueryArmor.Presentation.Notifications
{
    internal static class StatusBarNotifier
    {
        public static void ShowWarning(DTE2 dte, string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            dte.StatusBar.Text = message;
        }

        public static void ShowError(DTE2 dte, string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            dte.StatusBar.Text = message;
        }
    }
}
