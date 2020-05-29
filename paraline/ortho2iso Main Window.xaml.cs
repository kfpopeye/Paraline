using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace paraline
{
    /// <summary>
    /// Interaction logic for IsoMaker Main Window.xaml
    /// </summary>
    public partial class IsoMaker_Main_Window : Window
    {
        public enum UsersChoices { None, Top, Left, Right};
        public UsersChoices UsersChoice = UsersChoices.None;
        public bool outline = false;
        public bool delete = false;

        public IsoMaker_Main_Window(ElementSet s, Document d)
        {
            InitializeComponent();
        }

        void Top_Button_Click(object sender, RoutedEventArgs e)
        {
            UsersChoice = UsersChoices.Top;
            this.DialogResult = true;
            setBools();
            this.Close();
        }

        void Left_Button_Click(object sender, RoutedEventArgs e)
        {
            UsersChoice = UsersChoices.Left;
            this.DialogResult = true;
            setBools();
            this.Close();
        }

        void Right_Button_Click(object sender, RoutedEventArgs e)
        {
            UsersChoice = UsersChoices.Right;
            this.DialogResult = true;
            setBools();
            this.Close();
        }

        private void setBools()
        {
            if ((bool)CB_outline.IsChecked)
                outline = true;
            else
                outline = false;
            if ((bool)CB_delete.IsChecked)
                delete = true;
            else
                delete = false;
        }
    }
}