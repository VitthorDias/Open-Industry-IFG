using Godot;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class SceneConfigManager
{
	// ============================
	// ESTRUTURA DE DADOS
	// ============================
	
	public class SceneConfig
	{
		[JsonPropertyName("sensors")]
		public Dictionary<string, SensorConfig> Sensors { get; set; } = new();
		
		[JsonPropertyName("conveyors")]
		public Dictionary<string, ConveyorConfig> Conveyors { get; set; } = new();
		
		[JsonPropertyName("diverters")]
		public Dictionary<string, DiverterConfig> Diverters { get; set; } = new();
		
		// ✅ FUTURO: Adicionar mais componentes conforme necessário
		// public Dictionary<string, LightConfig> Lights { get; set; } = new();
		// public Dictionary<string, RobotConfig> Robots { get; set; } = new();
	}
	
	public class SensorConfig
	{
		[JsonPropertyName("rotationY")]
		public float RotationY { get; set; }
		
		[JsonPropertyName("distance")]
		public float? Distance { get; set; } // Opcional
		
		[JsonPropertyName("scanRate")]
		public float? ScanRate { get; set; } // Opcional
	}
	
	public class ConveyorConfig
	{
		[JsonPropertyName("fixedSpeed")]
		public float? FixedSpeed { get; set; }
		
		[JsonPropertyName("maxWeight")]
		public float? MaxWeight { get; set; } // Para BalanceConveyor
		
		[JsonPropertyName("signalType")]
		public string SignalType { get; set; } // Para BalanceConveyor
		
		[JsonPropertyName("beltColor")]
		public string BeltColor { get; set; } // Hex color
	}
	
	public class DiverterConfig
	{
		[JsonPropertyName("extendTime")]
		public float? ExtendTime { get; set; }
		
		[JsonPropertyName("retractTime")]
		public float? RetractTime { get; set; }
	}
	
	// ============================
	// CAMINHOS
	// ============================
	
	private static string ConfigsFolder
	{
		get
		{
			string exeDir = ProjectSettings.GlobalizePath("res://");
			string configPath = Path.Combine(exeDir, "configs");
			
			// Cria pasta se não existir
			if (!Directory.Exists(configPath))
			{
				Directory.CreateDirectory(configPath);
				GD.Print($"[SceneConfigManager] Pasta 'configs' criada em: {configPath}");
			}
			
			return configPath;
		}
	}
	
	public static string GetConfigPath(int scene)
	{
		return Path.Combine(ConfigsFolder, $"scene_config_{scene}.json");
	}
	
	// ============================
	// SALVAR / CARREGAR
	// ============================
	
	public static void SaveConfig(SceneConfig config, int scene)
	{
		try
		{
			string path = GetConfigPath(scene);
			
			var options = new JsonSerializerOptions 
			{ 
				WriteIndented = true,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			};
			
			string json = JsonSerializer.Serialize(config, options);
			File.WriteAllText(path, json);
			
			GD.Print($"[SceneConfigManager] ✅ Configuração salva: {path}");
		}
		catch (Exception e)
		{
			GD.PrintErr($"[SceneConfigManager] ❌ Erro ao salvar config:");
			GD.PrintErr(e.Message);
		}
	}
	
	public static SceneConfig LoadConfig(int scene)
	{
		try
		{
			string path = GetConfigPath(scene);
			
			if (!File.Exists(path))
			{
				GD.Print($"[SceneConfigManager] Nenhuma config encontrada para cena {scene}. Usando padrões.");
				return new SceneConfig();
			}
			
			string json = File.ReadAllText(path);
			var config = JsonSerializer.Deserialize<SceneConfig>(json);
			
			GD.Print($"[SceneConfigManager] ✅ Configuração carregada: {path}");
			return config ?? new SceneConfig();
		}
		catch (Exception e)
		{
			GD.PrintErr($"[SceneConfigManager] ❌ Erro ao carregar config:");
			GD.PrintErr(e.Message);
			return new SceneConfig();
		}
	}
	
	// ============================
	// MÉTODOS ESPECÍFICOS - SENSORS
	// ============================
	
	public static void SaveSensorRotation(string sensorName, float rotationY, int scene)
	{
		var config = LoadConfig(scene);
		
		if (!config.Sensors.ContainsKey(sensorName))
			config.Sensors[sensorName] = new SensorConfig();
		
		config.Sensors[sensorName].RotationY = rotationY;
		
		SaveConfig(config, scene);
		GD.Print($"[SceneConfigManager] Sensor '{sensorName}' rotação salva: {rotationY:F1}°");
	}
	
	public static float? LoadSensorRotation(string sensorName, int scene)
	{
		var config = LoadConfig(scene);
		
		if (config.Sensors.ContainsKey(sensorName))
		{
			return config.Sensors[sensorName].RotationY;
		}
		
		return null;
	}
	
	public static void RemoveSensorRotation(string sensorName, int scene)
	{
		var config = LoadConfig(scene);
		
		if (config.Sensors.ContainsKey(sensorName))
		{
			config.Sensors.Remove(sensorName);
			SaveConfig(config, scene);
			GD.Print($"[SceneConfigManager] Sensor '{sensorName}' removido (restaurado para padrão).");
		}
	}
	
	// ============================
	// MÉTODOS ESPECÍFICOS - CONVEYORS
	// ============================
	
	public static void SaveConveyorSpeed(string conveyorName, float speed, int scene)
	{
		var config = LoadConfig(scene);
		
		if (!config.Conveyors.ContainsKey(conveyorName))
			config.Conveyors[conveyorName] = new ConveyorConfig();
		
		config.Conveyors[conveyorName].FixedSpeed = speed;
		
		SaveConfig(config, scene);
		GD.Print($"[SceneConfigManager] Conveyor '{conveyorName}' velocidade salva: {speed:F2}");
	}
	
	public static float? LoadConveyorSpeed(string conveyorName, int scene)
	{
		var config = LoadConfig(scene);
		
		if (config.Conveyors.ContainsKey(conveyorName))
		{
			return config.Conveyors[conveyorName].FixedSpeed;
		}
		
		return null;
	}
	
	// ============================
	// RESTAURAR TUDO
	// ============================
	
	public static void RestoreAllConfigs(int scene)
	{
		try
		{
			string path = GetConfigPath(scene);
			
			if (File.Exists(path))
			{
				File.Delete(path);
				GD.Print($"[SceneConfigManager] ✅ TODAS as configurações da cena {scene} restauradas para padrão.");
			}
			else
			{
				GD.Print($"[SceneConfigManager] Nenhum arquivo de configuração encontrado para cena {scene}.");
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[SceneConfigManager] Erro ao restaurar configs:");
			GD.PrintErr(e.Message);
		}
	}
	
	public static void RestoreAllSensors(int scene)
	{
		var config = LoadConfig(scene);
		config.Sensors.Clear();
		SaveConfig(config, scene);
		GD.Print($"[SceneConfigManager] ✅ Todos os sensores da cena {scene} restaurados.");
	}
	
	public static void RestoreAllConveyors(int scene)
	{
		var config = LoadConfig(scene);
		config.Conveyors.Clear();
		SaveConfig(config, scene);
		GD.Print($"[SceneConfigManager] ✅ Todas as esteiras da cena {scene} restauradas.");
	}
}