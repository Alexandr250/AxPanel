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
        launchButton1 = new LaunchButtonView();
        SuspendLayout();
        // 
        // launchButton1
        // 
        launchButton1.Anchor = AnchorStyles.None;
        launchButton1.BaseControlPath = null;
        
        launchButton1.IsDragging = false;
        
        launchButton1.Location = new Point( 12, 12 );
        launchButton1.Name = "launchButton1";
        
        launchButton1.Size = new Size( 58, 53 );
        
        launchButton1.TabIndex = 0;
        launchButton1.Text = "launchButton1";
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF( 7F, 15F );
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size( 250, 223 );
        Controls.Add( launchButton1 );
        KeyPreview = true;
        Name = "Form1";
        Text = "Form1";
        KeyDown +=  Form1_KeyDown ;
        KeyUp +=  Form1_KeyUp ;
        ResumeLayout( false );
    }

    #endregion

    private LaunchButtonView launchButton1;
}
