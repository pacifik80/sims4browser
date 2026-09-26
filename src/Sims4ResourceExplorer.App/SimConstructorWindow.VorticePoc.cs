using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Sims4ResourceExplorer.App.Rendering.Vortice;

namespace Sims4ResourceExplorer.App;

/// <summary>
/// Proof-of-concept overlay that renders the current Sim head through the hand-written Vortice
/// D3D11 renderer (custom HLSL pixel shader + a live "Tint" slider constant) into a WinUI 3
/// <see cref="SwapChainPanel"/>, sitting on top of the existing Helix viewport. Toggling it ON
/// hides the Helix viewport and shows the Vortice surface; OFF tears it down and restores Helix.
/// The whole PoC lives in this partial so the production window code stays untouched.
/// </summary>
public sealed partial class SimConstructorWindow
{
    private SwapChainPanel? vorticePanel;
    private SwapChainPanelHost? vorticeHost;
    private SimHeadPocRenderer? vorticeRenderer;
    private Slider? vorticeTintSlider;
    private TextBlock? vorticeStatus;
    private bool vorticeLoopHooked;

    private void BuildVorticePocOverlay()
    {
        var toggle = new ToggleSwitch { Header = "Vortice D3D11", MinWidth = 0 };
        toggle.Toggled += VorticeToggle_Toggled;

        vorticeTintSlider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            StepFrequency = 0.01,
            Value = 1,
            Width = 180,
            Header = "Tint"
        };
        vorticeTintSlider.ValueChanged += (_, e) =>
        {
            if (vorticeRenderer is not null)
            {
                vorticeRenderer.DebugTint = (float)e.NewValue;
            }
        };

        vorticeStatus = new TextBlock
        {
            Text = string.Empty,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            Padding = new Thickness(12, 8, 12, 8)
        };
        panel.Children.Add(toggle);
        panel.Children.Add(vorticeTintSlider);
        panel.Children.Add(vorticeStatus);

        var chrome = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(170, 0, 0, 0)),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = panel
        };

        PreviewSurface.Children.Add(chrome); // top-most overlay (added last)
        Closed += (_, _) => TeardownVortice();
    }

    private void VorticeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch { IsOn: true })
        {
            EnableVortice();
        }
        else
        {
            DisableVortice();
        }
    }

    private void EnableVortice()
    {
        if (ViewModel.CurrentScene is null)
        {
            SetVorticeStatus("Load a Sim first.");
            return;
        }
        sceneViewport.Visibility = Visibility.Collapsed;

        vorticePanel = new SwapChainPanel();
        // Insert below the chrome overlay (added last) but above the now-collapsed Helix viewport.
        PreviewSurface.Children.Insert(0, vorticePanel);
        vorticePanel.Loaded += VorticePanel_Loaded;
        SetVorticeStatus("initializing…");
    }

    private async void VorticePanel_Loaded(object sender, RoutedEventArgs e)
    {
        var scene = ViewModel.CurrentScene;
        if (scene is null || vorticePanel is null)
        {
            return;
        }
        try
        {
            var pick = SimHeadData.PickHeadMeshAndSkin(scene);
            if (pick is null)
            {
                SetVorticeStatus("no skin atlas mesh in scene");
                return;
            }
            var (bgra, w, h) = await TextureLoader.DecodeBgraAsync(pick.Value.BaseColorPng);

            vorticeHost = new SwapChainPanelHost(vorticePanel);
            vorticeHost.Initialize();
            vorticeRenderer = new SimHeadPocRenderer(vorticeHost);
            vorticeRenderer.Load(scene, bgra, w, h);
            if (vorticeTintSlider is not null)
            {
                vorticeRenderer.DebugTint = (float)vorticeTintSlider.Value;
            }

            CompositionTarget.Rendering += Vortice_Rendering;
            vorticeLoopHooked = true;
            SetVorticeStatus($"D3D11 live · atlas {w}×{h}");
        }
        catch (Exception ex)
        {
            SetVorticeStatus($"ERROR: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[VorticePoC] {ex}");
        }
    }

    private void Vortice_Rendering(object? sender, object e) => vorticeRenderer?.Render();

    private void DisableVortice()
    {
        TeardownVortice();
        sceneViewport.Visibility = ViewModel.CurrentScene is null ? Visibility.Collapsed : Visibility.Visible;
        SetVorticeStatus(string.Empty);
    }

    private void TeardownVortice()
    {
        if (vorticeLoopHooked)
        {
            CompositionTarget.Rendering -= Vortice_Rendering;
            vorticeLoopHooked = false;
        }
        vorticeRenderer?.Dispose();
        vorticeRenderer = null;
        vorticeHost?.Dispose();
        vorticeHost = null;
        if (vorticePanel is not null)
        {
            PreviewSurface.Children.Remove(vorticePanel);
            vorticePanel = null;
        }
    }

    private void SetVorticeStatus(string text)
    {
        if (vorticeStatus is not null)
        {
            vorticeStatus.Text = text;
        }
    }
}
