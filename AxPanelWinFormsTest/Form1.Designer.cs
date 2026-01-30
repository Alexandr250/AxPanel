using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanelWinFormsTest;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose( bool disposing )
    {
        if ( disposing && ( components != null ) )
        {
            components.Dispose();
        }
        base.Dispose( disposing );
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        SuspendLayout();
        axPanelMainContainer1 = new AxPanelMainContainer( new DarkTheme() );
        // 
        // axPanelMainContainer1
        // 
        axPanelMainContainer1.BackColor = Color.FromArgb(     64,     64,     64 );
        axPanelMainContainer1.Dock = DockStyle.Fill;
        axPanelMainContainer1.Location = new Point( 0, 0 );
        axPanelMainContainer1.Name = "axPanelMainContainer1";
        axPanelMainContainer1.Size = new Size( 331, 622 );
        axPanelMainContainer1.TabIndex = 0;
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF( 7F, 15F );
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size( 331, 622 );
        Controls.Add( axPanelMainContainer1 );
        KeyPreview = true;
        Name = "Form1";
        Text = "Form1";
        KeyDown +=  Form1_KeyDown ;
        KeyUp +=  Form1_KeyUp ;
        ResumeLayout( false );
    }

    #endregion

    private AxPanelMainContainer axPanelMainContainer1;
}
