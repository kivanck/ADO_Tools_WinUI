namespace ADO_Tools.UI
{
    partial class FindSimilarForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FindSimilarForm));
            comboBox_CompareQuarry = new ComboBox();
            listView_CompareItems = new ListView();
            label_originalItem = new Label();
            button_FindItems = new Button();
            button_form2downloaditems = new Button();
            label_size = new Label();
            label_downloading = new Label();
            label_numberofItems = new Label();
            label4 = new Label();
            label3 = new Label();
            textBox_topMatchNumber = new TextBox();
            label1 = new Label();
            label2 = new Label();
            SuspendLayout();
            // 
            // comboBox_CompareQuarry
            // 
            comboBox_CompareQuarry.FormattingEnabled = true;
            comboBox_CompareQuarry.Location = new Point(14, 78);
            comboBox_CompareQuarry.Margin = new Padding(4, 3, 4, 3);
            comboBox_CompareQuarry.Name = "comboBox_CompareQuarry";
            comboBox_CompareQuarry.Size = new Size(507, 23);
            comboBox_CompareQuarry.TabIndex = 0;
            // 
            // listView_CompareItems
            // 
            listView_CompareItems.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            listView_CompareItems.Location = new Point(14, 112);
            listView_CompareItems.Margin = new Padding(4, 3, 4, 3);
            listView_CompareItems.Name = "listView_CompareItems";
            listView_CompareItems.Size = new Size(1117, 349);
            listView_CompareItems.TabIndex = 1;
            listView_CompareItems.UseCompatibleStateImageBehavior = false;
            listView_CompareItems.ColumnClick += listView_CompareItems_ColumnClick;
            listView_CompareItems.DoubleClick += listView_CompareItems_DoubleClick;
            // 
            // label_originalItem
            // 
            label_originalItem.AutoSize = true;
            label_originalItem.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label_originalItem.Location = new Point(128, 15);
            label_originalItem.Margin = new Padding(4, 0, 4, 0);
            label_originalItem.Name = "label_originalItem";
            label_originalItem.Size = new Size(369, 16);
            label_originalItem.TabIndex = 2;
            label_originalItem.Text = "Report for Crossover with double turnout feature is incomplete";
            // 
            // button_FindItems
            // 
            button_FindItems.Location = new Point(528, 78);
            button_FindItems.Margin = new Padding(4, 3, 4, 3);
            button_FindItems.Name = "button_FindItems";
            button_FindItems.Size = new Size(88, 27);
            button_FindItems.TabIndex = 3;
            button_FindItems.Text = "Find Items";
            button_FindItems.UseVisualStyleBackColor = true;
            button_FindItems.Click += button_FindItems_Click;
            // 
            // button_form2downloaditems
            // 
            button_form2downloaditems.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button_form2downloaditems.Location = new Point(1007, 76);
            button_form2downloaditems.Margin = new Padding(4, 3, 4, 3);
            button_form2downloaditems.Name = "button_form2downloaditems";
            button_form2downloaditems.Size = new Size(125, 27);
            button_form2downloaditems.TabIndex = 4;
            button_form2downloaditems.Text = "Download Items";
            button_form2downloaditems.UseVisualStyleBackColor = true;
            button_form2downloaditems.Click += button_form2downloaditems_Click;
            // 
            // label_size
            // 
            label_size.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label_size.AutoSize = true;
            label_size.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label_size.ForeColor = SystemColors.Highlight;
            label_size.Location = new Point(870, 33);
            label_size.Margin = new Padding(4, 0, 4, 0);
            label_size.Name = "label_size";
            label_size.Size = new Size(194, 18);
            label_size.TabIndex = 10;
            label_size.Text = "3 Attachments: 32.56 mb";
            // 
            // label_downloading
            // 
            label_downloading.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label_downloading.AutoSize = true;
            label_downloading.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label_downloading.ForeColor = SystemColors.Highlight;
            label_downloading.Location = new Point(869, 12);
            label_downloading.Margin = new Padding(4, 0, 4, 0);
            label_downloading.Name = "label_downloading";
            label_downloading.Size = new Size(173, 18);
            label_downloading.TabIndex = 9;
            label_downloading.Text = "Downloading #102654";
            // 
            // label_numberofItems
            // 
            label_numberofItems.AutoSize = true;
            label_numberofItems.Location = new Point(355, 60);
            label_numberofItems.Margin = new Padding(4, 0, 4, 0);
            label_numberofItems.Name = "label_numberofItems";
            label_numberofItems.Size = new Size(156, 15);
            label_numberofItems.TabIndex = 17;
            label_numberofItems.Text = "Top 5 matches are displayed";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label4.Location = new Point(840, 82);
            label4.Margin = new Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new Size(39, 16);
            label4.TabIndex = 20;
            label4.Text = "items";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label3.Location = new Point(709, 82);
            label3.Margin = new Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new Size(75, 16);
            label3.TabIndex = 19;
            label3.Text = "Display top";
            // 
            // textBox_topMatchNumber
            // 
            textBox_topMatchNumber.Location = new Point(798, 80);
            textBox_topMatchNumber.Margin = new Padding(4, 3, 4, 3);
            textBox_topMatchNumber.Name = "textBox_topMatchNumber";
            textBox_topMatchNumber.Size = new Size(34, 23);
            textBox_topMatchNumber.TabIndex = 18;
            textBox_topMatchNumber.Text = "5";
            textBox_topMatchNumber.TextAlign = HorizontalAlignment.Center;
            textBox_topMatchNumber.TextChanged += textBox_Days_TextChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(14, 60);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(90, 15);
            label1.TabIndex = 21;
            label1.Text = "Search items in;";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label2.Location = new Point(14, 15);
            label2.Margin = new Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new Size(91, 16);
            label2.TabIndex = 22;
            label2.Text = "Compare with;";
            // 
            // FindSimilarForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1146, 475);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(textBox_topMatchNumber);
            Controls.Add(label_numberofItems);
            Controls.Add(label_size);
            Controls.Add(label_downloading);
            Controls.Add(button_form2downloaditems);
            Controls.Add(button_FindItems);
            Controls.Add(label_originalItem);
            Controls.Add(listView_CompareItems);
            Controls.Add(comboBox_CompareQuarry);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(4, 3, 4, 3);
            Name = "FindSimilarForm";
            Text = "Find Similar Items";
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox comboBox_CompareQuarry;
        private System.Windows.Forms.ListView listView_CompareItems;
        private System.Windows.Forms.Label label_originalItem;
        private System.Windows.Forms.Button button_FindItems;
        private System.Windows.Forms.Button button_form2downloaditems;
        private System.Windows.Forms.Label label_size;
        private System.Windows.Forms.Label label_downloading;
        private System.Windows.Forms.Label label_numberofItems;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBox_topMatchNumber;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
    }
}