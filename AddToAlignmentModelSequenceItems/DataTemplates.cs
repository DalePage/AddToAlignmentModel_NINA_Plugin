using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using NINA.View.Sequencer;

namespace ADPUK.NINA.AddToAlignmentModel.AddToAlignmentModelSequenceItems {
    [Export(typeof(ResourceDictionary))]

    public partial class DataTemplates {
        public DataTemplates() {
            InitializeComponent();
        }
    }
}
