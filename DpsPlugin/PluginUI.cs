using ImGuiNET;
using System;
using System.Numerics;

namespace DpsPlugin
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private Configuration configuration;

        private ImGuiScene.TextureWrap goatImage;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private BigInteger totalDamage;
        public BigInteger TotalDamage {
            get { return this.totalDamage; }
            set { this.totalDamage = value; }
        }

        public Decimal DPS {
            get { return (Decimal)TotalDamage / (Math.Max(ElapsedSeconds, new Decimal(1.0))); }
        }

        private DateTime startTime;
        public DateTime StartTime {
            get { return this.startTime; }
            set { this.startTime = value; }
        }

        public Decimal ElapsedSeconds {
            get { return new Decimal((double)(DateTime.Now.Subtract(StartTime).TotalMilliseconds) / 1000.0d); }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        // passing in the image here just for simplicity
        public PluginUI(Configuration configuration, ImGuiScene.TextureWrap goatImage)
        {
            this.configuration = configuration;
            this.goatImage = goatImage;
            this.StartTime = DateTime.Now;
            this.totalDamage = 0;
        }

        public void Dispose()
        {
            this.goatImage.Dispose();
        }

        public void Draw()
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            DrawMainWindow();
            DrawSettingsWindow();
        }

        public void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 330), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("DPS", ref this.visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.Text(string.Format("DPS: {0:.##}", DPS));
                ImGui.Text(string.Format("Elapsed seconds: {0:.#}", ElapsedSeconds));
                // ImGui.Text($"The random config bool is {this.configuration.SomePropertyToBeSavedAndWithADefault}");

                if (ImGui.Button("Show Settings"))
                {
                    SettingsVisible = true;
                }

                // ImGui.Spacing();

                // ImGui.Text("Have a goat:");
                // ImGui.Indent(55);
                // ImGui.Image(this.goatImage.ImGuiHandle, new Vector2(this.goatImage.Width, this.goatImage.Height));
                // ImGui.Unindent(55);
            }
            ImGui.End();
        }

        public void DrawSettingsWindow()
        {
            if (!SettingsVisible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(232, 75), ImGuiCond.Always);
            if (ImGui.Begin("A Wonderful Configuration Window", ref this.settingsVisible,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                // can't ref a property, so use a local copy
                var configValue = this.configuration.SomePropertyToBeSavedAndWithADefault;
                if (ImGui.Checkbox("Random Config Bool", ref configValue))
                {
                    this.configuration.SomePropertyToBeSavedAndWithADefault = configValue;
                    // can save immediately on change, if you don't want to provide a "Save and Close" button
                    this.configuration.Save();
                }
            }
            ImGui.End();
        }
    }
}
