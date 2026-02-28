using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clash_WPF.ViewModels;

namespace Clash_WPF.Views;

public partial class ProxyView : UserControl
{
    public ProxyView()
    {
        InitializeComponent();
    }

    private void GroupTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ProxyGroupItem group
            && DataContext is ProxyViewModel vm)
        {
            vm.SelectedGroup = group;
        }
    }

    private void NodeCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ProxyNodeItem node
            && DataContext is ProxyViewModel vm)
        {
            vm.SelectNodeCommand.Execute(node);
        }
    }
}
