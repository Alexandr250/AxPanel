using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel;

partial class MainView
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
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        TimerClicksCounter = new System.Windows.Forms.Timer( components );
        SuspendLayout();
        // 
        // TimerClicksCounter
        // 
        TimerClicksCounter.Interval = 1000;
        TimerClicksCounter.Tick +=  TimerClicksCounter_Tick ;
        // 
        // MainView
        // 
        AutoScaleDimensions = new SizeF( 7F, 15F );
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(     64,     64,     64 );
        ClientSize = new Size( 247, 347 );
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        Name = "MainView";
        Padding = new Padding( 0, 30, 0, 0 );
        StartPosition = FormStartPosition.Manual;
        Text = "MainPanel";
        //FormClosed +=  Form1_FormClosed ;
        //ResizeEnd +=  Form1_ResizeEnd ;
        //Scroll +=  Form1_Scroll ;
        //Paint +=  MainView_Paint ;
        //KeyDown +=  MainView_KeyDown ;
        //KeyUp +=  MainView_KeyUp ;
        //MouseClick +=  Form1_MouseClick ;
        //MouseDown +=  Form1_MouseDown ;
        //MouseMove +=  Form1_MouseMove ;
        //MouseUp +=  Form1_MouseUp ;
        //Resize +=  Form1_Resize ;
        ResumeLayout( false );
    }

    #endregion

    private System.Windows.Forms.Timer TimerClicksCounter;
    public AxPanelMainContainer MainContainer;
}
