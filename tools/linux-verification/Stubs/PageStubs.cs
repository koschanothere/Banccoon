// Stand-ins for the XAML-generated half of each page's partial class.
namespace Banccoon.App.Views
{
    using Microsoft.Maui.Controls;

    public partial class ReconciliationPage { private void InitializeComponent() { } }
    public partial class StatementImportPage { private void InitializeComponent() { } }
    public partial class TransactionsPage { private void InitializeComponent() { } private Button ImportButton = new(); private Border AddMenu = new(); }
    public partial class DashboardPage { private void InitializeComponent() { } }
    public partial class AccountsPage { private void InitializeComponent() { } }
}

// App itself: the XAML-generated half, plus AppShell (a Shell in reality; only ever handed to Window).
namespace Banccoon.App
{
    public partial class App { private void InitializeComponent() { } }
    public class AppShell : Microsoft.Maui.Controls.Page { }
}
