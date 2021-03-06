﻿#region using

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

#endregion

namespace ExplorerWpf.CustomControls {
    /// <summary>
    ///     Interaction logic for SelectFolderTextBox.xaml
    /// </summary>
    public partial class SelectFolderTextBox : TextBox {
        private string _lastPath;

        private bool _loaded;


        private bool _prevState;

        public SelectFolderTextBox() { InitializeComponent(); }
        public Popup   Popup    => this.Template.FindName( "PART_Popup",    this ) as Popup;
        public ListBox ItemList => this.Template.FindName( "PART_ItemList", this ) as ListBox;

        public Grid Root => this.Template.FindName( "root", this ) as Grid;

        //12-25-08 : Add Ghost image when picking from ItemList
        //TextBlock TempVisual { get { return this.Template.FindName("PART_TempVisual", this) as TextBlock; } }
        public ScrollViewer Host => this.Template.FindName( "PART_ContentHost", this ) as ScrollViewer;

        public UIElement TextBoxView {
            get {
                foreach ( var o in LogicalTreeHelper.GetChildren( this.Host ) ) return o as UIElement;

                return null;
            }
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            this._loaded                   =  true;
            this.KeyDown                   += AutoCompleteTextBox_KeyDown;
            this.PreviewKeyDown            += AutoCompleteTextBox_PreviewKeyDown;
            this.ItemList.PreviewMouseDown += ItemList_PreviewMouseDown;
            this.ItemList.KeyDown          += ItemList_KeyDown;
            //TempVisual.MouseDown += new MouseButtonEventHandler(TempVisual_MouseDown);
            //09-04-09 Based on SilverLaw's approach 
            this.Popup.CustomPopupPlacementCallback += Repositioning;

            var parentWindow = GetParentWindow();

            if ( parentWindow == null ) return;

            parentWindow.Deactivated += delegate {
                this._prevState   = this.Popup.IsOpen;
                this.Popup.IsOpen = false;
            };
            parentWindow.Activated += delegate { this.Popup.IsOpen = this._prevState; };
        }

        private Window GetParentWindow() {
            DependencyObject d = this;
            while ( d != null && !( d is Window ) )
                d = LogicalTreeHelper.GetParent( d );
            return d as Window;
        }

        //09-04-09 Based on SilverLaw's approach 
        private CustomPopupPlacement[] Repositioning(Size popupSize, Size targetSize, Point offset) {
            return new[] {
                new CustomPopupPlacement( new Point( 0.01 - offset.X, this.Root.ActualHeight - offset.Y ), PopupPrimaryAxis.None )
            };
        }

        private void TempVisual_MouseDown(object sender, MouseButtonEventArgs e) {
            var text = this.Text;
            this.ItemList.SelectedIndex = -1;
            this.Text                   = text;
            this.Popup.IsOpen           = false;
        }

        private void AutoCompleteTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
            //12-25-08 - added PageDown Support
            if ( this.ItemList.Items.Count > 0 && !( e.OriginalSource is ListBoxItem ) )
                switch (e.Key) {
                    case Key.Up:
                    case Key.Down:
                    case Key.Prior:
                    case Key.Next:
                        this.ItemList.Focus();
                        this.ItemList.SelectedIndex = 0;
                        var lbi = this.ItemList.ItemContainerGenerator.ContainerFromIndex( this.ItemList.SelectedIndex ) as ListBoxItem;
                        lbi.Focus();
                        e.Handled = true;
                        break;
                }
        }


        private void ItemList_KeyDown(object sender, KeyEventArgs e) {
            if ( e.OriginalSource is ListBoxItem ) {
                var tb = e.OriginalSource as ListBoxItem;

                e.Handled = true;

                switch (e.Key) {
                    case Key.Enter:
                        this.Text = tb.Content as string;
                        UpdateSource();
                        break;
                    //12-25-08 - added "\" support when picking in list view
                    case Key.Oem5:
                        this.Text = ( tb.Content as string ) + "\\";
                        break;
                    //12-25-08 - roll back if escape is pressed
                    case Key.Escape:
                        this.Text = this._lastPath.TrimEnd( '\\' ) + "\\";
                        break;
                    default:
                        e.Handled = false;
                        break;
                }

                //12-25-08 - Force focus back the control after selected.
                if ( e.Handled ) {
                    Keyboard.Focus( this );
                    this.Popup.IsOpen = false;
                    Select( this.Text.Length, 0 ); //Select last char
                }
            }
        }


        private void AutoCompleteTextBox_KeyDown(object sender, KeyEventArgs e) {
            if ( e.Key == Key.Enter ) {
                this.Popup.IsOpen = false;
                UpdateSource();
            }
        }

        private void UpdateSource() {
            if ( GetBindingExpression( TextProperty ) != null )
                GetBindingExpression( TextProperty ).UpdateSource();
        }

        private void ItemList_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            if ( e.LeftButton != MouseButtonState.Pressed ) return;

            if ( !( e.OriginalSource is TextBlock tb ) ) return;

            this.Text = tb.Text;
            UpdateSource();
            this.Popup.IsOpen = false;
            e.Handled         = true;
        }

        protected override void OnTextChanged(TextChangedEventArgs e) {
            if ( this._loaded )
                try {
                    //if (lastPath != Path.GetDirectoryName(this.Text))
                    //if (textBox.Text.EndsWith("\\"))                        
                    {
                        this._lastPath = Path.GetDirectoryName( this.Text );
                        var paths = Lookup( this.Text );

                        this.ItemList.Items.Clear();
                        foreach ( var path in paths )
                            if ( !string.Equals( path, this.Text, StringComparison.CurrentCultureIgnoreCase ) )
                                this.ItemList.Items.Add( path );
                    }

                    this.Popup.IsOpen = this.ItemList.Items.Count > 0;

                    //ItemList.Items.Filter = p =>
                    //{
                    //    string path = p as string;
                    //    return path.StartsWith(this.Text, StringComparison.CurrentCultureIgnoreCase) &&
                    //        !(String.Equals(path, this.Text, StringComparison.CurrentCultureIgnoreCase));
                    //};
                } catch { }
        }


        private string[] Lookup(string path) {
            try {
                if ( Directory.Exists( Path.GetDirectoryName( path ) ) ) {
                    var lookupFolder = new DirectoryInfo( Path.GetDirectoryName( path ) );

                    if ( lookupFolder != null ) {
                        var allItems = lookupFolder.GetDirectories();
                        return ( from di in allItems where di.FullName.StartsWith( path, StringComparison.CurrentCultureIgnoreCase ) select di.FullName ).ToArray();
                    }
                }
            } catch (Exception ex) {
                SettingsHandler.OnError( ex );
            }

            return new string[0];
        }
    }
}
