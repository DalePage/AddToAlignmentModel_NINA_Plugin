using System.ComponentModel.Composition;
using System.Windows;

namespace ADPUK.NINA.AddToAlignmentModel.AddToAlignmentModelImageTab {
    [Export(typeof(ResourceDictionary))]

    public partial class AlignmentModelView {
        public AlignmentModelView() {
            InitializeComponent();
        }
    }
}
