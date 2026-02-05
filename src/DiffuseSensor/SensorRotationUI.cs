using Godot;
using System;

public partial class SensorRotationUI : Control
{
	private PanelContainer panel;
	private Label labelTitle;
	private HSlider slider;
	private Label labelValue;
	private Button btnClose;
	private Button btnSave;
	private Button btnRestore;
	
	private DiffuseSensor currentSensor;
	
	public override void _Ready()
	{
		// Criar UI
		panel = new PanelContainer();
		panel.Position = new Vector2(50, 50);
		panel.CustomMinimumSize = new Vector2(400, 150);
		AddChild(panel);
		
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 10);
		panel.AddChild(vbox);
		
		// Título
		labelTitle = new Label { Text = "Ajustar Rotação do Sensor" };
		labelTitle.AddThemeFontSizeOverride("font_size", 18);
		vbox.AddChild(labelTitle);
		
		var hbox = new HBoxContainer();
		vbox.AddChild(hbox);
		
		// Label "Rotação Y:"
		var labelRotY = new Label { Text = "Rotação Y:" };
		hbox.AddChild(labelRotY);
		
		// Slider
		slider = new HSlider();
		slider.MinValue = -180;  // ou 0
		slider.MaxValue = 180;   // ou 360
		slider.Step = 1;
		slider.Value = 0;
		slider.CustomMinimumSize = new Vector2(250, 0);
		slider.ValueChanged += OnSliderChanged;
		hbox.AddChild(slider);
		
		// Label do valor
		labelValue = new Label { Text = "0°" };
		labelValue.CustomMinimumSize = new Vector2(50, 0);
		hbox.AddChild(labelValue);
		
		// Botões
		var hboxButtons = new HBoxContainer();
		hboxButtons.AddThemeConstantOverride("separion", 10);
		vbox.AddChild(hboxButtons);
		
		btnSave = new Button { Text = "Salvar" };
		btnSave.Pressed += OnSavePressed;
		hboxButtons.AddChild(btnSave);
		
		btnRestore = new Button { Text = "Restaurar Padrão" };
		btnRestore.Pressed += OnRestorePressed;
		hboxButtons.AddChild(btnRestore);
		
		// Botão Fechar
		btnClose = new Button { Text = "Fechar" };
		btnClose.Pressed += OnClosePressed;
		vbox.AddChild(btnClose);
		
		// Inicialmente invisível
		Visible = false;
	}
	
	public void ShowForSensor(DiffuseSensor sensor)
	{
		GD.Print($"[SensorRotationUI] ShowForSensor chamado");
		GD.Print($"  - sensor: {(sensor != null ? "OK" : "NULL")}");
		GD.Print($"  - slider: {(slider != null ? "OK" : "NULL")}");
		GD.Print($"  - labelValue: {(labelValue != null ? "OK" : "NULL")}");
		GD.Print($"  - labelTitle: {(labelTitle != null ? "OK" : "NULL")}");
		GD.Print($"  - panel: {(panel != null ? "OK" : "NULL")}");
		
		currentSensor = sensor;
		
		if (slider == null)
		{
			GD.PrintErr("[SensorRotationUI] ❌ Slider está NULL! _Ready() não foi chamado ainda.");
			return;
		}
		
		if (currentSensor != null)
		{
			float currentRotation = currentSensor.GetRotationY();
			slider.Value = currentRotation;
			labelValue.Text = $"{currentRotation:F0}°";
			labelTitle.Text = $"Ajustar Rotação: {sensor.Name}";
			Visible = true;
		}
	}
	
	public new void Hide()
	{
		Visible = false;
		currentSensor = null;
	}
	
	private void OnSliderChanged(double value)
	{
		if (currentSensor != null)
		{
			float rotation = (float)value;
			currentSensor.SetRotationY(rotation, save: false); // save = false (só salva ao clicar botão)
			labelValue.Text = $"{rotation:F0}°";
		}
	}
	
	private void OnClosePressed()
	{
		Hide();
	}
	
	public override void _Input(InputEvent @event)
	{
		// Fecha ao pressionar ESC
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
		{
			if (Visible)
			{
				Hide();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	private void OnSavePressed()
	{
		if (currentSensor != null)
		{
			float currentRotation = (float)slider.Value;
			currentSensor.SetRotationY(currentRotation, save: true); // save = true
			
			GD.Print($"[SensorRotationUI] Rotação salva: {currentSensor.Name} = {currentRotation:F1}°");

			string originalText = btnSave.Text;
			btnSave.Text = "Salvo!";
			
			GetTree().CreateTimer(1.0).Timeout += () => {
				if (IsInstanceValid(btnSave))
					btnSave.Text = originalText;
			};
		}
	}

	private void OnRestorePressed()
	{
		if (currentSensor != null)
		{
			// Restaura rotação original
			currentSensor.RestoreOriginalRotation();
			
			// Atualiza UI
			float newRotation = currentSensor.GetRotationY();
			slider.Value = newRotation;
			labelValue.Text = $"{newRotation:F0}°";
			
			GD.Print($"[SensorRotationUI] Rotação restaurada: {currentSensor.Name} = {newRotation:F1}°");
		}
	}
}