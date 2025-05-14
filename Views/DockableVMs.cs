using System.ComponentModel.Composition;
using System.Windows;

namespace ADPUK.NINA.AddToAlignmentModel.AddToAlignmentModelImageTab {
    [Export(typeof(ResourceDictionary))]

    public partial class AlignmentModelVM {
        public AlignmentModelVM() {
            InitializeComponent();
        }
    }
}
