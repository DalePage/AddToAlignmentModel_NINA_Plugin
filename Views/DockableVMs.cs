using NINA.WPF.Base.ViewModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Windows;
using System;
using System.Windows.Controls;
using System.Threading;

namespace ADPUK.NINA.AddToAlignmentModel.AddToAlignmentModelImageTab {
    [Export(typeof(ResourceDictionary))]

    public partial class AlignmentModelVM {
        public AlignmentModelVM() {
            InitializeComponent();
        }
        private void CreateModelClick(object sender, EventArgs e) {
            try {
                CreateAlignmentModelVM createAlignmentModelVM = (sender as Button).DataContext as CreateAlignmentModelVM;
                createAlignmentModelVM.ExecuteCreate().GetAwaiter();
            } catch { }
        }
        private void CancelModelClick(object sender, EventArgs e) {
            try {
                CreateAlignmentModelVM createAlignmentModelVM = (sender as Button).DataContext as CreateAlignmentModelVM;
                createAlignmentModelVM.CancelTokenSource.Cancel();
            } catch { }
        }

    }

}
