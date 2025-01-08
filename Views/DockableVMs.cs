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
    }
}
