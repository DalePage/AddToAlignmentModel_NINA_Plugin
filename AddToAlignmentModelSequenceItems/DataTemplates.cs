using System.ComponentModel.Composition;
using System.Windows;

namespace ADPUK.NINA.AddToAlignmentModel.AddToAlignmentModelSequenceItems {
    [Export(typeof(ResourceDictionary))]

    public partial class DataTemplates {
        public DataTemplates() {
            InitializeComponent();
        }
    }
}
