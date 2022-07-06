
namespace OmegaScript.Scripts
{
    partial class strainHalf
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
            this.oneMLDelvBtn = new System.Windows.Forms.Button();
            this.twoMLDelvBtn = new System.Windows.Forms.Button();
            this.question = new System.Windows.Forms.Label();
            this.halfDelBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // oneMLDelvBtn
            // 
            this.oneMLDelvBtn.Cursor = System.Windows.Forms.Cursors.Hand;
            this.oneMLDelvBtn.Location = new System.Drawing.Point(291, 91);
            this.oneMLDelvBtn.Name = "oneMLDelvBtn";
            this.oneMLDelvBtn.Size = new System.Drawing.Size(157, 74);
            this.oneMLDelvBtn.TabIndex = 0;
            this.oneMLDelvBtn.Text = "1 mL";
            this.oneMLDelvBtn.UseVisualStyleBackColor = true;
            this.oneMLDelvBtn.Click += new System.EventHandler(this.oneMLDelvBtn_Click);
            // 
            // twoMLDelvBtn
            // 
            this.twoMLDelvBtn.Cursor = System.Windows.Forms.Cursors.Hand;
            this.twoMLDelvBtn.Location = new System.Drawing.Point(529, 91);
            this.twoMLDelvBtn.Name = "twoMLDelvBtn";
            this.twoMLDelvBtn.Size = new System.Drawing.Size(157, 74);
            this.twoMLDelvBtn.TabIndex = 1;
            this.twoMLDelvBtn.Text = "2 mL";
            this.twoMLDelvBtn.UseVisualStyleBackColor = true;
            this.twoMLDelvBtn.Click += new System.EventHandler(this.twoMLDelvBtn_Click);
            // 
            // question
            // 
            this.question.AutoSize = true;
            this.question.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.875F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.question.Location = new System.Drawing.Point(36, 23);
            this.question.Name = "question";
            this.question.Size = new System.Drawing.Size(673, 42);
            this.question.TabIndex = 2;
            this.question.Text = "Volume within the Disruption Chamber?\r\n";
            // 
            // halfDelBtn
            // 
            this.halfDelBtn.Cursor = System.Windows.Forms.Cursors.Hand;
            this.halfDelBtn.Location = new System.Drawing.Point(43, 91);
            this.halfDelBtn.Name = "halfDelBtn";
            this.halfDelBtn.Size = new System.Drawing.Size(157, 74);
            this.halfDelBtn.TabIndex = 3;
            this.halfDelBtn.Text = "0.5 mL";
            this.halfDelBtn.UseVisualStyleBackColor = true;
            this.halfDelBtn.Click += new System.EventHandler(this.halfDelBtn_Click);
            // 
            // strainHalf
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.CornflowerBlue;
            this.ClientSize = new System.Drawing.Size(751, 200);
            this.Controls.Add(this.halfDelBtn);
            this.Controls.Add(this.question);
            this.Controls.Add(this.twoMLDelvBtn);
            this.Controls.Add(this.oneMLDelvBtn);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "strainHalf";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "strainHalf";
            this.TopMost = true;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button oneMLDelvBtn;
        private System.Windows.Forms.Button twoMLDelvBtn;
        private System.Windows.Forms.Label question;
        private System.Windows.Forms.Button halfDelBtn;
    }
}