namespace OpenUtauMobile.Controls;

public partial class ConfirmPopup : PopupDialogControl
{
    protected override PopupDialogWidthPreset WidthPreset => PopupDialogWidthPreset.Compact;

    public ConfirmPopup()
    {
        InitializeComponent();
    }
}
