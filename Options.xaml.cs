using System.ComponentModel.Composition;
using System.Windows;

namespace ADPUK.NINA.AddToAlignmentModel {
    [Export(typeof(ResourceDictionary))]
    partial class Options : ResourceDictionary {

        public Options() {
            InitializeComponent();
        }
    }
}