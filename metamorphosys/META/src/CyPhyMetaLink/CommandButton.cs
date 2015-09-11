﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CyPhyMetaLink
{
    public partial class CommandLink : Button
    {
        const int BS_COMMANDLINK = 0x0000000E;

        public CommandLink()
        {
            this.FlatStyle = FlatStyle.System;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cParams = base.CreateParams;
                cParams.Style |= BS_COMMANDLINK;
                return cParams;
            }
        }
    }
}
