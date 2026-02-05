using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class SceneComponents
{
	public static List<SceneComponents> sceneOneComponents { get; } = new List<SceneComponents>
	{
		// This order is inportant to link wich tag was selected for each component
		// In the screen, first is listed inputs, then outputs

		// INPUTS
		new SceneComponents(0, "DiffuseSensor", "Sensor 1", "", "input"),
		new SceneComponents(1, "DiffuseSensor2", "Sensor 2", "", "input"),
		new SceneComponents(2, "PushButton", "Botão Desligar", "", "input"),
		new SceneComponents(3, "PushButton2", "Botão Ligar", "", "input"),

		// OUTPUTS
		new SceneComponents(4, "Conveyor", "Esteira 1", "", "output"),
		new SceneComponents(5, "Conveyor2", "Esteira 2", "", "output"),
		new SceneComponents(6, "BoxSpawner", "Gerador Caixas", "", "output"),
	};

	public static List<SceneComponents> sceneTwoComponents { get; } = new List<SceneComponents>
	{
		// This order is inportant to link wich tag was selected for each component
		// In the screen, first is listed inputs, then outputs

		// INPUTS
		new SceneComponents(0, "DiffuseSensor", "Sensor 1", "", "input"),
		new SceneComponents(1, "DiffuseSensor2", "Sensor 2", "", "input"),
		new SceneComponents(2, "PushButton", "Botão Saída 1", "", "input"),
		new SceneComponents(3, "PushButton2", "Botão Saída 2", "", "input"),
		new SceneComponents(4, "PushButton3", "Botão Desligar", "", "input"),
		new SceneComponents(5, "PushButton4", "Botão Ligar", "", "input"),
		
		// OUTPUTS
		new SceneComponents(6, "Diverter", "Pistão 1", "", "output"),
		new SceneComponents(7, "BladeStop", "Obstáculo 1", "", "output"),
		new SceneComponents(8, "Diverter2", "Pistão 2", "", "output"),
		new SceneComponents(9, "BladeStop2", "Obstáculo 2", "", "output"),
		new SceneComponents(10, "BoxSpawner", "Gerador Caixas", "", "output"),
	};

	public static List<SceneComponents> sceneThreeComponents { get; } = new List<SceneComponents>
	{
		// This order is inportant to link wich tag was selected for each component
		// In the screen, first is listed inputs, then outputs

		// INPUTS
		new SceneComponents(0, "DiffuseSensor1", "Sensor 1", "", "input"),
		new SceneComponents(1, "DiffuseSensor2", "Sensor 2", "", "input"),
		new SceneComponents(2, "DiffuseSensor3", "Sensor 3", "", "input"),
		new SceneComponents(3, "DiffuseSensor4", "Sensor 4", "", "input"),
		new SceneComponents(4, "DiffuseSensor5", "Sensor 5", "", "input"),
		new SceneComponents(5, "PushButton1", "Botão Ligar", "", "input"),
		new SceneComponents(6, "PushButton2", "Botão Desligar", "", "input"),
		
		// OUTPUTS
		new SceneComponents(7, "BoxSpawner", "Gerador Caixas", "", "output"),
		new SceneComponents(8, "Conveyor1", "Esteira 1", "", "output"),
		new SceneComponents(9, "Conveyor2", "Esteira 2", "", "output"),
		new SceneComponents(10, "Conveyor3", "Esteira 3", "", "output"),
		new SceneComponents(11, "Conveyor4", "Esteira 4", "", "output"),
		new SceneComponents(12, "Conveyor5", "Esteira 5", "", "output"),
	};

	public static List<SceneComponents> sceneFourComponents { get; } = new List<SceneComponents>
	{
		// This order is inportant to link wich tag was selected for each component
		// In the screen, first is listed inputs, then outputs

		// INPUTS
		new SceneComponents(0, "DiffuseSensor", "Sensor 1", "", "input"),
		new SceneComponents(1, "DiffuseSensor2", "Sensor 2", "", "input"),
		new SceneComponents(2, "PushButton", "Botão Saída 1", "", "input"),
		new SceneComponents(3, "PushButton3", "Botão Desligar", "", "input"),
		new SceneComponents(4, "PushButton4", "Botão Ligar", "", "input"),
		
		// OUTPUTS
		new SceneComponents(5, "Diverter", "Pistão 1", "", "output"),
		new SceneComponents(6, "BladeStop", "Obstáculo 1", "", "output"),
		new SceneComponents(7, "BladeStop2", "Obstáculo 2", "", "output"),
		new SceneComponents(8, "BoxSpawner", "Gerador Caixas", "", "output"),
	};
	
	public static List<SceneComponents> sceneFiveComponents { get; } = new List<SceneComponents>
	{
		// This order is inportant to link wich tag was selected for each component
		// In the screen, first is listed inputs, then outputs

		// INPUTS
		new SceneComponents(0, "PushButton-1", "Botão Ligar", "", "input"),
		new SceneComponents(1, "PushButton-2", "Botão Desligar", "", "input"),
		new SceneComponents(2, "PushButton-3", "Botão Resetar", "", "input"),
		new SceneComponents(3, "DiffuseSensor-1", "Sensor Entrada Est Prin", "", "input"),
		new SceneComponents(4, "DiffuseSensor-2", "Sensor Saída Est Prin", "", "input"),
		new SceneComponents(5, "DiffuseSensor-3", "Sensor Saída Est 1", "", "input"),
		new SceneComponents(6, "DiffuseSensor-4", "Sensor Saída Est 3", "", "input"),
		new SceneComponents(7, "DiffuseSensor-5", "Sensor Saída Est Rolo", "", "input"),
		new SceneComponents(8, "DiffuseSensor-6", "Sensor Saída Est 4", "", "input"),
		new SceneComponents(9, "DiffuseSensor-7", "Sensor Saída Est 5", "", "input"),
		//new SceneComponents(10, "DiffuseSensor-8", "Sensor Saída Est 7", "", "input"),
		new SceneComponents(10, "DiffuseSensor-8", "Sensor Saída Linha C", "", "input"),
		new SceneComponents(11, "DiffuseSensor-9", "Sensor Eject A", "", "input"),
		new SceneComponents(12, "DiffuseSensor-10", "Sensor Eject B", "", "input"),
		new SceneComponents(13, "DiffuseSensor-11", "Sensor Entrada Linha A", "", "input"),
		new SceneComponents(14, "DiffuseSensor-12", "Sensor Saída Linha A", "", "input"),
		new SceneComponents(15, "DiffuseSensor-13", "Sensor Entrada Linha B", "", "input"),
		new SceneComponents(16, "DiffuseSensor-14", "Sensor Saída Linha B", "", "input"),
		new SceneComponents(17, "PushButton-4", "Botão Linha A", "", "input"),
		new SceneComponents(18, "PushButton-5", "Botão Linha B", "", "input"),
		new SceneComponents(19, "PushButton-6", "Botão Linha C", "", "input"),

		
		// OUTPUTS
		new SceneComponents(20, "Conveyor-1", "Esteira Entrada Prin", "", "output"),
		new SceneComponents(21, "Conveyor-2", "Esteira 1", "", "output"),
		new SceneComponents(22, "Conveyor-3", "Esteira 2", "", "output"),
		new SceneComponents(23, "CurvedBeltConveyor-1", "Esteira 3", "", "output"),
		new SceneComponents(24, "Conveyor-4", "Esteira 4", "", "output"),
		new SceneComponents(25, "Conveyor-5", "Esteira 5", "", "output"),
		// new SceneComponents(23, "Conveyor-6", "Esteira 6", "", "output"),
		new SceneComponents(26, "Conveyor-6", "Esteira Linha A", "", "output"),
		new SceneComponents(27, "Conveyor-7", "Esteira Linha B", "", "output"),
		new SceneComponents(28, "Conveyor-8", "Esteira Linha C", "", "output"),
		new SceneComponents(29, "Diverter-1", "Cil Ejetor A", "", "output"),
		new SceneComponents(30, "Diverter-2", "Cil Ejetor B", "", "output"),
		new SceneComponents(31, "BoxSpawner-1", "Gerador Caixas", "", "output"),
	};

	public static List<SceneComponents> sceneSixComponents { get; } = new List<SceneComponents>
	{
		// This order is inportant to link wich tag was selected for each component
		// In the screen, first is listed inputs, then outputs

		// INPUTS
		new SceneComponents(0, "Botao-1", "Ligar", "", "input"),
		new SceneComponents(1, "Botao-2", "Desligar", "", "input"),
		new SceneComponents(2, "Botao-3", "Reset", "", "input"),
		new SceneComponents(3, "Sensor-E1", "Sensor Entrada", "", "input"),
		new SceneComponents(4, "Sensor-E2", "Sensor Sai Ent", "", "input"),
		new SceneComponents(5, "Sensor-E3", "Sensor Pos Balan", "", "input"),
		new SceneComponents(6, "EsteiraBalanca-1", "Balanca Peso", "", "input"),
		new SceneComponents(7, "Sensor-E4", "Sensor Sai Balan", "", "input"),
		new SceneComponents(8, "Sensor-E5", "Sensor Pos Ejec-1", "", "input"),
		new SceneComponents(9, "Sensor-E6", "Sensor Sai Ejec 1", "", "input"),
		new SceneComponents(10, "Sensor-E7", "Sensor Pos Ejec-2", "", "input"),
		new SceneComponents(11, "Sensor-E8", "Sensor Sai Ejec 2", "", "input"),
		new SceneComponents(12, "Eject-1", "FC-Avanco Eject 1", "", "input"),
		new SceneComponents(13, "Eject-1", "FC-Recuo Eject 1", "", "input"),
		new SceneComponents(14, "Eject-2", "FC-Avanco Eject 2", "", "input"),
		new SceneComponents(15, "Eject-2", "FC-Recuo Eject 2", "", "input"),

		// OUTPUTS
		new SceneComponents(16, "Esteira-1", "Esteira Entrada", "", "output"),
		new SceneComponents(17, "EsteiraBalanca-1", "Esteira Balanca", "", "output"),
		new SceneComponents(18, "Esteira-2", "Esteira Ejetora 1", "", "output"),
		new SceneComponents(19, "Eject-1", "Ejetora 1", "", "output"),
		new SceneComponents(20, "Esteira-3", "Esteira Ejetora 2", "", "output"),
		new SceneComponents(21, "Eject-2", "Ejetora 2", "", "output"),
	};

	// Propriedades da classe
	public int Id { get; set; }
	public string Key { get; set; }
	public string Name { get; set; }
	public string Tag { get; set; }
	public string Type { get; set; }
	// Construtor
	public SceneComponents(int id, string key, string name, string tag, string type)
	{
		Id = id;
		Key = key;
		Name = name;
		Tag = tag;
		Type = type;
	}

	public static SceneComponents GetComponentByKey(string key, int scene)
	{
		SceneComponents component = null;

		switch (scene)
		{
			case 1:
				component = sceneOneComponents.FirstOrDefault(f => f.Key.ToLower() == key.ToLower());
				break;

			case 2:
				component = sceneTwoComponents.FirstOrDefault(f => f.Key.ToLower() == key.ToLower());
				break;

			case 3:
				component = sceneThreeComponents.FirstOrDefault(f => f.Key.ToLower() == key.ToLower());
				break;
			
			case 4:
				component = sceneFourComponents.FirstOrDefault(f => f.Key.ToLower() == key.ToLower());
				break;

			case 5:
				component = sceneFiveComponents.FirstOrDefault(f => f.Key.ToLower() == key.ToLower());
				break;

			case 6:
				component = sceneSixComponents.FirstOrDefault(f => f.Key.ToLower() == key.ToLower());
				break;
			
			default:
				break;			
		}

		if(component != null)
			return component;
		else
			return new SceneComponents(99, "", "", "", "");
	}

	public static SceneComponents GetComponentById(int id, int scene)
	{
		switch (scene)
		{
			case 1: return sceneOneComponents.FirstOrDefault(f => f.Id == id);
			case 2: return sceneTwoComponents.FirstOrDefault(f => f.Id == id);
			case 3: return sceneThreeComponents.FirstOrDefault(f => f.Id == id);
			case 4: return sceneFourComponents.FirstOrDefault(f => f.Id == id);
			case 5: return sceneFiveComponents.FirstOrDefault(f => f.Id == id);
			case 6: return sceneSixComponents.FirstOrDefault(f => f.Id == id);
			default: return null;
		}
	}
}
