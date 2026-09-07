# UI Theming Rules for SS14-MIDI-IDE

When creating new WPF windows in this project, **do not** use default Windows styling. You **MUST** use the custom dark theme styling established in `PreferencesWindow.xaml` and `ChangelogWindow.xaml`.

## Mandatory Window Styling Template
All new windows must include the following properties on the `<Window>` tag:
```xml
Background="#1e1e1e"
Foreground="White"
WindowStyle="None"
AllowsTransparency="True"
ResizeMode="NoResize"
WindowStartupLocation="CenterOwner"
```

## Mandatory Window Structure
The root content of the window must be a Border that implements the custom title bar:

```xml
<Border CornerRadius="5" Background="#2d2d30" Padding="10">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        
        <!-- Custom Title Bar Header -->
        <DockPanel Grid.Row="0" Height="30" Background="#3e3e42" MouseLeftButtonDown="Header_MouseLeftButtonDown">
            <TextBlock Text="Window Title Here" Foreground="White" VerticalAlignment="Center" Margin="10,0" FontWeight="Bold"/>
            
            <!-- Close Button -->
            <Button Width="30" Height="30" HorizontalAlignment="Right" Margin="0,0,5,0" Click="CloseButton_Click" Foreground="White">
                <Button.Template>
                    <ControlTemplate TargetType="Button">
                        <Border Name="border" Background="Transparent" CornerRadius="3">
                            <Path Name="icon" Data="M 0,0 L 10,10 M 10,0 L 0,10" Stroke="#999" StrokeThickness="2" VerticalAlignment="Center" HorizontalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#e81123"/>
                                <Setter TargetName="icon" Property="Stroke" Value="White"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Button.Template>
            </Button>
        </DockPanel>
        
        <!-- Window Content Goes Here -->
        <Grid Grid.Row="1">
            ...
        </Grid>
    </Grid>
</Border>
```

## Mandatory Code-Behind
Your `Window.xaml.cs` file must implement the drag logic and close logic:

```csharp
private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
    {
        DragMove();
    }
}

private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
{
    Close();
}
```
