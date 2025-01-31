using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TeacherToolbox.Controls
{
    public sealed partial class ExpanderItem : UserControl
    {
        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(ExpanderItem), new PropertyMetadata(string.Empty));

        public string Subheader
        {
            get => (string)GetValue(SubheaderProperty);
            set => SetValue(SubheaderProperty, value);
        }

        public static readonly DependencyProperty SubheaderProperty =
            DependencyProperty.Register(nameof(Subheader), typeof(string), typeof(ExpanderItem), new PropertyMetadata(string.Empty));

        public object ItemContent
        {
            get => GetValue(ItemContentProperty);
            set => SetValue(ItemContentProperty, value);
        }

        public static readonly DependencyProperty ItemContentProperty =
            DependencyProperty.Register(nameof(ItemContent), typeof(object), typeof(ExpanderItem), new PropertyMetadata(null));

        public bool HasSubheader => !string.IsNullOrEmpty(Subheader);

        public ExpanderItem()
        {
            this.InitializeComponent();
        }
    }
}