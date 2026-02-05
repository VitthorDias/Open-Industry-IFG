using Godot;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Opc;
using OpcRcw.Dx;
using GodotPlugins.Game;
using libplctag;

public partial class CommsConfig : Control
{
	// Lista para armazenar os servidores OPC
	private ItemList il_opcServerList;

	// Lista global de servidores OPC
	List<Opc.Da.Server> globalOpcServerList = new List<Opc.Da.Server>();

	// Lista para tags OPC encontradas
	private ItemList il_opcServerTagList;

	// Container com lista de Tags e OptionButtons
	private VBoxContainer vBoxContainerTagsInput;
	private VBoxContainer vBoxContainerTagsOutput;

	List<string> opc_da_items_list = new List<string>();

	static Opc.Da.Server opcServer;
	static private Opc.Da.Subscription subscription;
	static Opc.Da.Item[] opc_da_items_array;

	private bool tagsModificadas = false;

	public Root Main { get; set; }

	// MenuButton
	private MenuButton menuConfig;
	Control commsConfigMenu;
	PanelContainer simulation_Control;

	int currentScene;
	List<SceneComponents> sceneComponents;

	// const string CONFIG_PATH = "user://opc_config.json";
	public string SceneConfigPath
	{
		get
		{
			// Caminho físico da pasta onde o executável está
			string exeDir = ProjectSettings.GlobalizePath("res://");

			// Pasta de configs
			string configPath = System.IO.Path.Combine(exeDir, "configs");

			// Criar Pasta caso não exista
			if (!System.IO.Directory.Exists(configPath))
			{
				System.IO.Directory.CreateDirectory(configPath);
			}

			return System.IO.Path.Combine(configPath, $"opc_config_scene_{Main.currentScene}.json");
		}
	}

	[Export] public bool DebugOpcEvents = false;
	[Export] public string TagPrefix = "FIO"; // Filtro para tags

	// .
	public override void _Ready()
	{
		try
		{
			GD.Print("\n> [CommsConfig.cs] [_Ready()]");
			Main = GetTree().CurrentScene as Root;
			currentScene = Main.currentScene;

			Inicializacao_UI();
			Inicializacao_SceneComponents();
			Configuracao_MenuButton();
			LoadSavedConfig(); // Carrega configuração salva
		}
		catch (Exception e)
		{
			GD.PrintErr("Erro CommsConfig.cs - _Ready");
			GD.PrintErr(e);
		}		
	}

	private void Inicializacao_UI()
	{
		// Ocultando itens antes da conexão com o servidor OPC
		GetNode<ScrollContainer>("MarginContainer/HBoxContainer/VBoxContainer1/ScrollContainer").Visible = false;
		GetNode<RichTextLabel>("MarginContainer/HBoxContainer/VBoxContainer1/RichTextTagsFound").Visible = false;
		GetNode<VBoxContainer>("MarginContainer/HBoxContainer/VBoxContainerMap").Visible = false;

		// Lista de servidores
		il_opcServerList = GetNode<ItemList>("MarginContainer/HBoxContainer/VBoxContainer1/ServerList");
		il_opcServerList.Visible = false;

		// Lista de tags do servidor OPC
		il_opcServerTagList = GetNode<ItemList>("MarginContainer/HBoxContainer/VBoxContainer1/ScrollContainer/VBoxContainer/OpcServerTagList");
		vBoxContainerTagsInput = GetNode<VBoxContainer>("MarginContainer/HBoxContainer/VBoxContainerMap/ScrollContainerTagsInput/VBoxContainerTagsInput");
		vBoxContainerTagsOutput = GetNode<VBoxContainer>("MarginContainer/HBoxContainer/VBoxContainerMap/ScrollContainerTagsOutputs/VBoxContainerTagsOutput");
	}

	private void Inicializacao_SceneComponents()
	{
		switch (currentScene)
		{
			case 1: sceneComponents = SceneComponents.sceneOneComponents; break;
			case 2: sceneComponents = SceneComponents.sceneTwoComponents; break;
			case 3: sceneComponents = SceneComponents.sceneThreeComponents; break;
			case 4: sceneComponents = SceneComponents.sceneFourComponents; break;
			case 5: sceneComponents = SceneComponents.sceneFiveComponents; break;
			case 6: sceneComponents = SceneComponents.sceneSixComponents; break;
			default: sceneComponents = null; break;
		}

		foreach (var component in sceneComponents)
		{
			HBoxContainer hbox = new HBoxContainer { 
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, 
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin 
			};
			hbox.AddChild(new Label
			{
				Text = component.Name,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
			});

			OptionButton opt = new OptionButton
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
			};

			// Guarda o id do componente dentro do OptionButton para sabermos qual componente atualizar
			opt.SetMeta("component_id", component.Id);

			opt.ItemSelected += (index) => OnOptionSelected(opt, index);
			hbox.AddChild(opt);

			if (component.Type == "input")
			{
				vBoxContainerTagsInput.AddChild(hbox);
			}
			else
			{
				vBoxContainerTagsOutput.AddChild(hbox);
			}
		}
	}

	private void Configuracao_MenuButton()
	{
		// Configurações do MenuButton
		var menuConfig = GetNode<MenuButton>("MenuConfig");
		menuConfig.GetPopup().AddItem("Voltar"); // ID: 0
		menuConfig.GetPopup().AddSeparator();
		menuConfig.GetPopup().AddItem("Menu Inicial"); // ID: 2
		menuConfig.GetPopup().AddSeparator();
		menuConfig.GetPopup().AddItem("Desconectar"); // ID: 4
		menuConfig.GetPopup().AddSeparator();
		menuConfig.GetPopup().AddItem("Restaurar TAGs"); // ID: 6
		menuConfig.GetPopup().AddSeparator();
		menuConfig.GetPopup().AddItem("Sair"); // ID: 8

		menuConfig.GetPopup().IdPressed += OnItemPressed;
	}

	// Função do MenuButton
	private void OnItemPressed(long id)
	{
		// A partir de cada ID dentro do MenuButton realiza sua função.
		// Os IDs são inteiros pares somente!
		switch (id)
		{
			case 0:
				GD.Print("VOLTAR");
				commsConfigMenu = GetNode<Control>("../CommsConfigMenu");
				simulation_Control = GetNode<PanelContainer>("../Simulation_Control");
				commsConfigMenu.Visible = !commsConfigMenu.Visible;
				simulation_Control.Visible = !simulation_Control.Visible;
				break;

			case 2:
				GD.Print("MENU INICIAL");
				disconnectOpcServer();
				GetTree().ChangeSceneToFile("res://src/MenuInicial/menu_inicial.tscn");
				break;			

			case 4:
				GD.Print("DESCONECTAR");
				disconnectOpcServer();
				break;

			case 6:
				GD.Print("RESTAURAR TAGs");
				RestaurarTagsOpc();
				break;

			case 8:
				GD.Print("SAIR");
				GetTree().Quit();
				break;
		}
	}

	public override void _ExitTree()
	{
		SaveConfig();
		
		if (opcServer != null && opcServer.IsConnected)
		{
			disconnectOpcServer();
		}

		base._ExitTree();
	}

	private void disconnectOpcServer()
	{
		try
		{
			GD.Print("Desconectando do servidor OPC DA.");

			if (subscription != null)
			{
				subscription.DataChanged -= OnDataChange;
				subscription.Dispose();
				subscription = null;
				GD.Print("├─ Subscription fechado.");
			}

			if (opcServer != null && opcServer.IsConnected)
			{
				opcServer.Disconnect();
				GD.Print("├─ Desconectado do servidor OPC.");
			}

			opcServer = null;

			var globalVariables = GetNodeOrNull("/root/GlobalVariables");
			if (globalVariables != null)
			{
				globalVariables.Set("opc_da_connected", false);
				GD.Print($"- globalVariables.opc_da_connected: {globalVariables.Get("opc_da_connected")}");
			}

			// Limpeza da interface
			il_opcServerTagList?.Clear();
			il_opcServerList?.Clear();
			ClearAllOptionButtons();

			GD.Print($"Conexão OPC encerrada com sucesso! opcServer: {opcServer}\n");
		}
		catch (Exception e)
		{
			GD.PrintErr("Erro ao tentar desconectar do servidor OPC DA: ");
			GD.PrintErr(e);
		}
	}
	
	private void ClearAllOptionButtons()
	{
		foreach (Node child in vBoxContainerTagsInput.GetChildren())
		{
			if (child is HBoxContainer hbox)
			{
				var opt = hbox.GetChild<OptionButton>(1);
				if (opt != null)
				{
					opt.Clear();
					opt.Selected = -1;
				}
			}
		}

		foreach (Node child in vBoxContainerTagsOutput.GetChildren())
		{
			if (child is HBoxContainer hbox)
			{
				var opt = hbox.GetChild<OptionButton>(1);
				if (opt != null)
				{
					opt.Clear();
					opt.Selected = -1;
				}
			}
		}
	}

	private void RestaurarTagsOpc()
	{
		// Diálogo de confirmação
		var dialog = new AcceptDialog
		{
			DialogText = "Isso irá DESMAPEAR todas as tags OPC da cena atual.\n" +
						"A conexão OPC DA e as tags descobertas serão mantida.\n" +
						"Quer continuar?",
			Title = "Confirmar Restauração de Tags"
		};
		
		dialog.Confirmed += () => {
			try
			{
				int count = 0;
				if (sceneComponents != null)
				{
					foreach (var comp in sceneComponents)
					{
						if (!string.IsNullOrEmpty(comp.Tag))
						{
							GD.Print($"  ├─ {comp.Name}: '{comp.Tag}' → ''");
							comp.Tag = "";
							count++;
						}
					}
				}
				
				// 2. Reseta OptionButtons para primeira posição (vazio)
				ResetarOptionButtons();
				
				// 3. Salva configuração (com tags vazias)
				SaveConfig();
				
				// Mostra mensagem de sucesso
				var successDialog = new AcceptDialog
				{
					DialogText = $"{count} tags desmapeadas com sucesso!\n\n" +
								"Configure novamente as tags nos OptionButtons.",
					Title = "Sucesso"
				};
				AddChild(successDialog);
				successDialog.PopupCentered();

				tagsModificadas = true;
				
				GD.Print($"[CommsConfig] {count} componentes desmapeados. Tags salvas como vazias no JSON.");
			}
			catch (Exception e)
			{
				GD.PrintErr("[CommsConfig] Erro ao desmapear tags OPC:");
				GD.PrintErr(e.Message);
				
				var errorDialog = new AcceptDialog
				{
					DialogText = $"Erro ao desmapear tags:\n\n{e.Message}",
					Title = "Erro"
				};
				AddChild(errorDialog);
				errorDialog.PopupCentered();
			}
		};
		
		AddChild(dialog);
		dialog.PopupCentered();
	}

	// Reseta OptionButtons para primeira posição
	private void ResetarOptionButtons()
	{
		int countInputs = 0;
		int countOutputs = 0;
		
		// INPUTS
		foreach (Node child in vBoxContainerTagsInput.GetChildren())
		{
			if (child is HBoxContainer hBox)
			{
				var opt = hBox.GetChild<OptionButton>(1);
				if (opt != null && opt.ItemCount > 0)
				{
					// Seleciona primeira opção (placeholder vazio)
					opt.Select(0);
					countInputs++;
					
					// Limpa tag do componente associado
					if (opt.HasMeta("component_id"))
					{
						int metaId = (int)opt.GetMeta("component_id");
						var comp = SceneComponents.GetComponentById(metaId, currentScene);
						if (comp != null)
							comp.Tag = "";
					}
				}
			}
		}
		
		// OUTPUTS
		foreach (Node child in vBoxContainerTagsOutput.GetChildren())
		{
			if (child is HBoxContainer hBox)
			{
				var opt = hBox.GetChild<OptionButton>(1);
				if (opt != null && opt.ItemCount > 0)
				{
					// Seleciona primeira opção (placeholder vazio)
					opt.Select(0);
					countOutputs++;
					
					// Limpa tag do componente associado
					if (opt.HasMeta("component_id"))
					{
						int metaId = (int)opt.GetMeta("component_id");
						var comp = SceneComponents.GetComponentById(metaId, currentScene);
						if (comp != null)
							comp.Tag = "";
					}
				}
			}
		}
		
		GD.Print($"[CommsConfig] OptionButtons resetados: {countInputs} inputs + {countOutputs} outputs");
	}

	// Busca os servidores OPC disponíveis
	private void _on_btn_opc_pressed()
	{
		try
		{
			if (opcServer != null && opcServer.IsConnected)
			{
				GD.Print("\n> [CommsConfig.cs] [_on_btn_opc_pressed()]");
				GD.Print("> Servidor já conectado!");
				return;
			}

			//Find opc server on local machine
			Opc.IDiscovery discovery = new OpcCom.ServerEnumerator();
			Opc.Server[] opcServerList = discovery.GetAvailableServers(Opc.Specification.COM_DA_30);

			if (il_opcServerList != null)
				il_opcServerList.Clear();
			
			if (il_opcServerTagList != null)
				il_opcServerTagList.Clear();
		
			if (globalOpcServerList != null)
				globalOpcServerList.Clear();
			else
				globalOpcServerList = new List<Opc.Da.Server>();

			GD.Print(discovery.GetAvailableServers(Opc.Specification.COM_DA_30));

			foreach (Opc.Server item in opcServerList)
			{
				il_opcServerList.AddItem(item.Name);
				globalOpcServerList.Add((Opc.Da.Server)item);
			}

			il_opcServerList.Visible = true;
		}
		catch (Exception e)
		{
			GD.PrintErr("Erro ao comunicar com OPC DA");
			GD.PrintErr(e);
		}
	}

	// Seleciona o servidor, conecta e busca as tags
	private void _on_server_selected(int index)
	{
		try
		{
			// Obter o nome do item selecionado
			string selectedItem = il_opcServerList.GetItemText(index);

			opcServer = globalOpcServerList.FirstOrDefault(server => server.Name.Equals(selectedItem));

			if (opcServer == null)
			{
				GD.PrintErr("Servidor não encontrado!");
				return;
			}

			// Conectar ao servidor
			opcServer.Connect();
			GD.Print("Conectado ao servidor OPC!");

			// Limpa listas ANTES do Browse
			il_opcServerTagList.Clear();
			opc_da_items_list.Clear();
			ClearAllOptionButtons();

			// Pesquisa todas as TAGs (agora SÓ descobre, não preenche UI)
			BrowsAllElement("", ref opcServer);

			// Preenche ItemList com TODAS as tags (completas)
			PreencherItemList();

			// Preenche OptionButtons com tags filtradas e simplificadas
			PreencherTodosOptionButtons();

			Create_Subscription();

			// Exibindo itens antes da conexão com o servidor OPC
			GetNode<ScrollContainer>("MarginContainer/HBoxContainer/VBoxContainer1/ScrollContainer").Visible = true;
			GetNode<RichTextLabel>("MarginContainer/HBoxContainer/VBoxContainer1/RichTextTagsFound").Visible = true;
			GetNode<VBoxContainer>("MarginContainer/HBoxContainer/VBoxContainerMap").Visible = true;

			var globalVariables = GetNodeOrNull("/root/GlobalVariables");
			if (globalVariables != null)
				globalVariables.Set("opc_da_connected", true);

			// Agora sincroniza configuração salva com as tags que acabamos de descobrir
			// (LoadSavedConfig aplica as seleções já usando os textos; SyncTagLists atualiza listas)
			LoadSavedConfig();   // carrega e aplica (se existir)
			SyncTagLists();      // remove itens inexistentes e adiciona novos
			SaveConfig();        // atualiza o arquivo com o cache atual (fonte de verdade = sceneComponents)

			GD.Print($"- globalVariables.opc_da_connected: {globalVariables?.Get("opc_da_connected")}");
		}
		catch (Exception ex)
		{
			GD.PrintErr("Erro ao conectar: ");
			GD.PrintErr(ex.ToString());
		}
	}

	private void Create_Subscription()
	{
		// Verificar se já existe subscription
		if (subscription != null)
		{
			GD.Print("Subscription já existe.");
			return;
		}
		
		GD.Print("Criando subscription.");

		Opc.Da.SubscriptionState state = new Opc.Da.SubscriptionState
		{
			Name = "OIIFG",
			Active = true,
			UpdateRate = 100,
			Deadband = 0,
		};

		subscription = (Opc.Da.Subscription)opcServer.CreateSubscription(state);

		// Coleta apenas tags que estão MAPEADAS nos componentes
		HashSet<string> tagsEmUso = new HashSet<string>();
		
		if (sceneComponents != null)
		{
			foreach (var comp in sceneComponents)
			{
				if (!string.IsNullOrEmpty(comp.Tag))
				{
					tagsEmUso.Add(comp.Tag);
				}
			}
		}

		if (tagsEmUso.Count == 0)
		{
			GD.PrintErr("Nenhuma tag mapeada! Subscription criada vazia.");
			GD.PrintErr("Configure as tags nos OptionButtons antes de iniciar a simulação.");
			// Mesmo assim cria subscription vazia para não dar erro depois
			opc_da_items_array = new Opc.Da.Item[0];
			return;
		}

		// Cria subscription APENAS com tags mapeadas
		opc_da_items_array = new Opc.Da.Item[tagsEmUso.Count];
		int i = 0;
		foreach (string tag in tagsEmUso)
		{
			opc_da_items_array[i] = new Opc.Da.Item
			{
				ClientHandle = Guid.NewGuid().ToString(),
				ItemPath = null,
				ItemName = tag
			};
			i++;
		}

		subscription.AddItems(opc_da_items_array);
		subscription.DataChanged += OnDataChange;

		// Força leitura inicial de todos os valores
		subscription.Refresh();

		GD.Print($"Subscription criada com {tagsEmUso.Count} tags mapeadas (de {opc_da_items_list.Count} tags disponíveis)");
		
		// Log de quais tags estão sendo monitoradas
		if (tagsEmUso.Count <= 20)
		{
			GD.Print("Tags monitoradas:");
			foreach (var tag in tagsEmUso)
			{
				GD.Print($"  - {tag}");
			}
		}
	}

	// Atualiza subscription quando tags são remapeadas
	public void AtualizarSubscription()
	{
		try
		{
			GD.Print("Atualizando subscription...");
			
			// Se já existe subscription válida, apenas atualiza os itens
			if (subscription != null)
			{
				GD.Print("  ├─ Subscription existente encontrada, atualizando itens...");
				
				// Remove evento temporariamente
				subscription.DataChanged -= OnDataChange;
				
				// Remove todos os itens antigos
				try
				{
					if (opc_da_items_array != null && opc_da_items_array.Length > 0)
					{
						subscription.RemoveItems(opc_da_items_array);
						GD.Print($"  ├─ {opc_da_items_array.Length} itens antigos removidos");
					}
				}
				catch (Exception e)
				{
					GD.Print($"  ├─ Aviso ao remover itens: {e.Message}");
				}
			}
			
			// Coleta tags mapeadas
			HashSet<string> tagsEmUso = new HashSet<string>();
			
			if (sceneComponents != null)
			{
				foreach (var comp in sceneComponents)
				{
					if (!string.IsNullOrEmpty(comp.Tag))
					{
						tagsEmUso.Add(comp.Tag);
					}
				}
			}

			if (tagsEmUso.Count == 0)
			{
				GD.PrintErr("  └─ Nenhuma tag mapeada! Subscription não atualizada.");
				return;
			}

			// Cria novos itens
			opc_da_items_array = new Opc.Da.Item[tagsEmUso.Count];
			int i = 0;
			foreach (string tag in tagsEmUso)
			{
				opc_da_items_array[i] = new Opc.Da.Item
				{
					ClientHandle = Guid.NewGuid().ToString(),
					ItemPath = null,
					ItemName = tag
				};
				i++;
			}

			// Adiciona novos itens à subscription existente
			subscription.AddItems(opc_da_items_array);
			
			// Reconecta evento
			subscription.DataChanged += OnDataChange;
			
			// Força leitura inicial
			subscription.Refresh();
			
			GD.Print($"  └─ {tagsEmUso.Count} tags mapeadas adicionadas");
			
			// Log de tags (opcional)
			if (tagsEmUso.Count <= 20)
			{
				GD.Print("     Tags monitoradas:");
				foreach (var tag in tagsEmUso)
				{
					GD.Print($"       - {tag}");
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("Erro ao atualizar subscription:");
			GD.PrintErr(e.Message);
			GD.PrintErr(e.StackTrace);
		}
	}

	void OnDataChange(object subscriptionHandle, object requestHandle, Opc.Da.ItemValueResult[] values)
	{
		// var receiveTime = DateTime.UtcNow;
		
		GD.Print($"📡 OnDataChange disparado! {values.Length} valores recebidos");
		
		foreach (var value in values)
		{
			// GD.Print($"  ├─ Tag: {value.ItemName}");
			// GD.Print($"  ├─ Valor: {value.Value}");
			// GD.Print($"  ├─ Timestamp Servidor: {value.Timestamp}"); // ✅ ADICIONE
			// GD.Print($"  ├─ Hora Recebimento: {receiveTime}"); // ✅ ADICIONE
			// GD.Print($"  ├─ Quality: {value.Quality}"); // ✅ ADICIONE
			
			// Só calcula se timestamp for válido
			// if (value.Timestamp != DateTime.MinValue && value.Timestamp > DateTime.MinValue.AddYears(10))
			// {
			// 	double latencyMs = (receiveTime - value.Timestamp).TotalMilliseconds;
			// 	GD.Print($"  └─ Latência OPC: {latencyMs:F2}ms");
			// }
			// else
			// {
			// 	GD.Print($"  └─ ⚠️ Timestamp inválido ou não fornecido");
			// }

			if (DebugOpcEvents)
			{
				GD.Print($"{value.ItemName} = {value.Value} (Q: {value.Quality})");
			}
			
			Main?.NotifyOpcDataChanged(value.ItemName, value.Value);
		}
	}

	private void PropagateOpcData(string tagName, object value)
	{
		Main?.NotifyOpcDataChanged(tagName, value);
		
		// Opcional: log para debug
		// GD.Print($"📡 OPC Update: {tagName} = {value}");
	}

	private void BrowsAllElement(string itemName, ref Opc.Da.Server opcServer)
	{
		try
		{
			Opc.Da.BrowseFilters filters = new Opc.Da.BrowseFilters();
			Opc.Da.BrowsePosition position = null;
			Opc.ItemIdentifier id = new Opc.ItemIdentifier(itemName);
			Opc.Da.BrowseElement[] children = opcServer.Browse(id, filters, out position);

			if (children == null)
			{
				return;
			}

			foreach (var item in children)
			{
				if (item.HasChildren)
				{
					string tagPath = string.IsNullOrEmpty(itemName) ? item.Name : $"{itemName}.{item.Name}";
					BrowsAllElement(tagPath, ref opcServer);
				} 
				else
				{
					string tagPath = $"{itemName}.{item.Name}";
					
					// Apenas adiciona à lista (sem preencher UI)
					if (!opc_da_items_list.Contains(tagPath))
					{
						opc_da_items_list.Add(tagPath);
					}
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("\n Erro BrowsAllElement(): ");
			GD.PrintErr(e);
		}
	}
	
	// Filtra tags baseado no prefixo
	private List<string> FiltrarTagsPorPrefixo(List<string> tags, string prefix)
	{
		if (string.IsNullOrEmpty(prefix))
			return tags;
		
		return tags.Where(tag => 
		{
			// Pega apenas o último segmento da tag (após último ponto)
			string[] parts = tag.Split('.');
			string lastPart = parts[parts.Length - 1];
			return lastPart.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
		}).ToList();
	}

	// Extrai apenas o nome final da tag (sem caminho completo)
	private string ExtrairNomeSimplificado(string tagCompleta)
	{
		// Tag completa: PLC_GW3.Application.IOs_FACTORY_IO.FIO_Q16_
		// Tag simplificada: FIO_Q16_
		string[] parts = tagCompleta.Split('.');
		return parts[parts.Length - 1];
	}

	// Preenche ItemList com tags completas
	private void PreencherItemList()
	{
		il_opcServerTagList.Clear();

		var tagsFiltradas = FiltrarTagsPorPrefixo(opc_da_items_list, TagPrefix);
		
		foreach (var tag in tagsFiltradas)
		{
			il_opcServerTagList.AddItem(tag); // Tag completa
		}
		
		GD.Print($"[CommsConfig] {opc_da_items_list.Count} tags carregadas no ItemList");
	}

	// Preenche OptionButtons com tags filtradas e simplificadas
	private void PreencherTodosOptionButtons()
	{
		// Filtra tags pelo prefixo
		var tagsFiltradas = FiltrarTagsPorPrefixo(opc_da_items_list, TagPrefix);
		
		GD.Print($"[CommsConfig] {tagsFiltradas.Count} tags filtradas com prefixo '{TagPrefix}'");
		
		// Preenche INPUTS
		PreencherOptionButtons(vBoxContainerTagsInput, tagsFiltradas);
		
		// Preenche OUTPUTS
		PreencherOptionButtons(vBoxContainerTagsOutput, tagsFiltradas);
	}

	// Preenche OptionButtons de um container específico
	private void PreencherOptionButtons(VBoxContainer container, List<string> tags)
	{
		foreach (Node child in container.GetChildren())
		{
			if (child is HBoxContainer hBox)
			{
				var optionButton = hBox.GetChild<OptionButton>(1);
				
				if (optionButton != null)
				{
					optionButton.Clear();
					
					// Adiciona placeholder vazio
					optionButton.AddItem("");
					optionButton.Select(-1);
					
					// Adiciona tags simplificadas
					foreach (var tagCompleta in tags)
					{
						string nomeSimplificado = ExtrairNomeSimplificado(tagCompleta);
						optionButton.AddItem(nomeSimplificado);
						
						// Guarda referência completa no metadata (índice atual = ItemCount - 1)
						optionButton.SetItemMetadata(optionButton.ItemCount - 1, tagCompleta);
					}
				}
			}
		}
	}

	private void _on_tag_selected(int index)
	{
		// GD.Print("\n> [CommsConfig.cs] [_on_tag_selected()]");
		// GD.Print($"- Tag selecionada: {il_opcServerTagList.GetItemText(index)}");
		// GD.Print($"- Valor: {ReadOpcItem(il_opcServerTagList.GetItemText(index))}");
	}

	// Recebe o nome da tag selecionada e atualiza
	private void OnOptionSelected(OptionButton optButton, long index)
	{
		try
		{
			// ID do componente está no metadata do OptionButton
			if (!optButton.HasMeta("component_id"))
				return;

			int componentId = (int)optButton.GetMeta("component_id");
			int selIndex = (int)index;

			// Se selecionou placeholder vazio
			if (selIndex < 0 || selIndex >= optButton.ItemCount)
			{
				// Limpa a tag do componente
				var cNull = SceneComponents.GetComponentById(componentId, currentScene);
				if (cNull != null) cNull.Tag = "";
				tagsModificadas = true;
				return;
			}

			// Pega tag completa do metadata do ITEM selecionado
			string tagCompleta = "";
			
			Variant metadata = optButton.GetItemMetadata(selIndex);
			if (metadata.Obj != null)
			{
				tagCompleta = metadata.ToString();
			}
			
			// Se não achou no metadata, tenta reconstruir (fallback)
			if (string.IsNullOrEmpty(tagCompleta))
			{
				string nomeSimplificado = optButton.GetItemText(selIndex);
				
				// Procura tag completa na lista
				tagCompleta = opc_da_items_list.FirstOrDefault(t => 
					ExtrairNomeSimplificado(t) == nomeSimplificado
				) ?? nomeSimplificado;
			}

			// Atribui tag completa ao componente
			var component = SceneComponents.GetComponentById(componentId, currentScene);
			if (component != null)
			{
				component.Tag = tagCompleta;
				tagsModificadas = true;
				GD.Print($"[CommsConfig] {component.Name}: {tagCompleta}");
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("Erro em OnOptionSelected: " + e.Message);
		}
	}

	// Faz uma leitura de uma tag específica no servidor OPC
	public static object ReadOpcItem(string tagName)
	{
		try
		{
			if (opcServer == null || !opcServer.IsConnected)
			{
				GD.PrintErr("Servidor OPC DA não conectado");
				return null;
			}
			
			Opc.Da.Item item = new Opc.Da.Item { ItemName = tagName };
			Opc.Da.ItemValueResult[] results = opcServer.Read(new [] { item });

			if (results[0].Value == null)
			{
				GD.Print($"Tag {tagName} retornou NULL");
			}

			return results[0].Value;
		}
		catch (Exception err)
		{
			GD.PrintErr("### CoomsConfig.cs - ReadOpcItem() - Erro ao ler a TAG");
			GD.PrintErr(err);
			return null;
		}
	}

	// Escreve os valores nas tags OPC
	public static void WriteOpcItem(string tagName, bool value)
	{
		try
		{
			if (opcServer == null || !opcServer.IsConnected)
			{
				GD.PrintErr($"Servidor OPC DA não conectado. Não foi possível escrever: {tagName}");
				return;
			}

			Opc.Da.Item opcItem = new Opc.Da.Item
			{
				ItemName = tagName,
				ItemPath = null,
				ClientHandle = Guid.NewGuid().ToString()
			};

			Opc.Da.ItemValue opcItemValue = new Opc.Da.ItemValue(opcItem)
			{
				Value = value
			};
			
			Opc.IdentifiedResult[] results = opcServer.Write(new [] { opcItemValue });

			// Verifica resultado
			if (results[0].ResultID.Failed())
			{
				GD.PrintErr($"Falha ao escrever {tagName}: {results[0].ResultID}");
			}
		}
		catch (Exception err)
		{
			GD.PrintErr("### CoomsConfig.cs - WriteOpcItem() bool: ");
			GD.PrintErr(err);
		}
	}

	public static void WriteOpcItem(string tagName, float value)
	{
		try
		{
			if (opcServer == null || !opcServer.IsConnected)
			{
				GD.PrintErr($"Servidor OPC DA não conectado. Não foi possível escrever: {tagName}");
				return;
			}

			Opc.Da.Item opcItem = new Opc.Da.Item
			{
				ItemName = tagName,
				ItemPath = null,
				ClientHandle = Guid.NewGuid().ToString()
			};

			Opc.Da.ItemValue opcItemValue = new Opc.Da.ItemValue(opcItem)
			{
				Value = value
			};
			
			Opc.IdentifiedResult[] results = opcServer.Write(new [] { opcItemValue });

			if (results[0].ResultID.Failed())
			{
				GD.PrintErr($"Falha ao escrever {tagName}: {results[0].ResultID}");
			}
		}
		catch (Exception err)
		{
			GD.PrintErr("### CoomsConfig.cs - WriteOpcItem() float: ");
			GD.PrintErr(err);
		}
	}

	// Save/Load Config - usando System.Text.Json
	private class SavedConfig
	{
		public Dictionary<string, string> Inputs { get; set; } = new();
		public Dictionary<string, string> Outputs { get; set; } = new();
		public string ServerName { get; set; }
	}

	private void SaveConfig()
	{
		var config = new SavedConfig();
		config.ServerName = opcServer?.Name;

		// SALVAR usando source-of-truth: sceneComponents
		if (sceneComponents != null)
		{
			foreach (var comp in sceneComponents)
			{
				if (comp == null) continue;
				string key = comp.Name ?? $"id_{comp.Id}";
				string value = comp.Tag ?? "";

				if (comp.Type == "input")
					config.Inputs[key] = value;
				else
					config.Outputs[key] = value;
			}
		}
		else
		{
			// fallback: se por algum motivo sceneComponents for null, tente salvar via OptionButtons (existente comportamento)
			foreach (Node node in vBoxContainerTagsInput.GetChildren())
			{
				if (!(node is HBoxContainer h)) continue;
				var label = h.GetChild<Label>(0).Text;
				var opt = h.GetChild<OptionButton>(1);

				if (opt == null || opt.ItemCount == 0 || opt.Selected < 0 || opt.Selected >= opt.ItemCount)
					config.Inputs[label] = "";
				else
					config.Inputs[label] = opt.GetItemText(opt.Selected);
			}

			foreach (Node node in vBoxContainerTagsOutput.GetChildren())
			{
				if (!(node is HBoxContainer h)) continue;
				var label = h.GetChild<Label>(0).Text;
				var opt = h.GetChild<OptionButton>(1);

				if (opt == null || opt.ItemCount == 0 || opt.Selected < 0 || opt.Selected >= opt.ItemCount)
					config.Outputs[label] = "";
				else
					config.Outputs[label] = opt.GetItemText(opt.Selected);
			}
		}

		string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
		string path = ProjectSettings.GlobalizePath(SceneConfigPath);
		File.WriteAllText(path, json);

		GD.Print($"Configurações salvas para cena {Main.currentScene}.");

		if (tagsModificadas && opcServer != null && opcServer.IsConnected)
		{
			GD.Print("[CommsConfig] Tags modificadas detectadas. Atualizando subscription...");
			AtualizarSubscription();
			tagsModificadas = false; // Reset flag
		}
	}

	private void LoadSavedConfig()
	{
		string path = ProjectSettings.GlobalizePath(SceneConfigPath);

		if (!File.Exists(path))
		{
			GD.Print("Nenhuma configuração salva encontrada.");
			return;
		}

		try
		{
			string json = File.ReadAllText(path);
			var loaded = JsonSerializer.Deserialize<SavedConfig>(json);

			if (loaded != null)
			{
				ApplyLoadedConfig(loaded);
				GD.Print($"Configurações carregadas da cena {Main.currentScene}.");
			}
			else
			{
				GD.Print("Arquivo de configuração inválido.");
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("Erro ao carregar configuração: " + e.Message);
		}
	}

	private void ApplyLoadedConfig(SavedConfig cfg)
	{
		// Aplicar INPUTS
		foreach (Node node in vBoxContainerTagsInput.GetChildren())
		{
			if (!(node is HBoxContainer h)) continue;
			string label = h.GetChild<Label>(0).Text;

			if (!cfg.Inputs.ContainsKey(label)) continue;

			string savedTag = cfg.Inputs[label];

			// tenta selecionar no OptionButton (se a tag estiver presente)
			SelectTagOnOptionButton(h.GetChild<OptionButton>(1), savedTag);

			// mantém a informação na estrutura de componentes (fonte de verdade)
			var metaId = h.GetChild<OptionButton>(1).HasMeta("component_id") ? (int)h.GetChild<OptionButton>(1).GetMeta("component_id") : -1;
			if (metaId != -1)
			{
				var comp = SceneComponents.GetComponentById(metaId, currentScene);
				if (comp != null)
					comp.Tag = savedTag;
			}
		}

		// Aplicar OUTPUTS
		foreach (Node node in vBoxContainerTagsOutput.GetChildren())
		{
			if (!(node is HBoxContainer h)) continue;
			string label = h.GetChild<Label>(0).Text;

			if (!cfg.Outputs.ContainsKey(label)) continue;

			string savedTag = cfg.Outputs[label];

			SelectTagOnOptionButton(h.GetChild<OptionButton>(1), savedTag);

			var metaId = h.GetChild<OptionButton>(1).HasMeta("component_id") ? (int)h.GetChild<OptionButton>(1).GetMeta("component_id") : -1;
			if (metaId != -1)
			{
				var comp = SceneComponents.GetComponentById(metaId, currentScene);
				if (comp != null)
					comp.Tag = savedTag;
			}
		}
	}

	private void SelectTagOnOptionButton(OptionButton opt, string tag)
	{
		if (opt == null || string.IsNullOrEmpty(tag))
		{
			return;
		}
		
		// Buscar pela tag completa no metadata
		for (int i = 0; i < opt.ItemCount; i++)
		{
			Variant metadata = opt.GetItemMetadata(i);
			if (metadata.Obj != null && metadata.ToString() == tag)
			{
				opt.Select(i);

				if (opt.HasMeta("component_id"))
				{
					int metaId = (int)opt.GetMeta("component_id");
					var comp = SceneComponents.GetComponentById(metaId, currentScene);
					if (comp != null) comp.Tag = tag;
				}

				return;
			}
		}
	}

	private void SyncTagLists()
	{
		foreach (Node node in vBoxContainerTagsInput.GetChildren())
		{
			if (node is HBoxContainer h)
				SyncOpButton(h.GetChild<OptionButton>(1));
		}

		foreach (Node node in vBoxContainerTagsOutput.GetChildren())
		{
			if (node is HBoxContainer h)
				SyncOpButton(h.GetChild<OptionButton>(1));
		}
	}

	private void SyncOpButton(OptionButton opt)
	{
		if (opt == null)
		{
			return;
		}

		// Guarda tag selecionada (do metadata)
		string selectedTag = null;
		if (opt.Selected >= 0 && opt.Selected < opt.ItemCount)
		{
			Variant metadata = opt.GetItemMetadata(opt.Selected);
			if (metadata.Obj != null)
				selectedTag = metadata.ToString();
		}

		// Recriar OptionButton com tags filtradas
		var tagsFiltradas = FiltrarTagsPorPrefixo(opc_da_items_list, TagPrefix);
		
		opt.Clear();
		opt.AddItem(""); // Placeholder
		
		foreach (var tagCompleta in tagsFiltradas)
		{
			string nomeSimplificado = ExtrairNomeSimplificado(tagCompleta);
			opt.AddItem(nomeSimplificado);
			opt.SetItemMetadata(opt.ItemCount - 1, tagCompleta);
		}

		// Re-selecionar tag anterior
		if (!string.IsNullOrEmpty(selectedTag))
		{
			for (int i = 0; i < opt.ItemCount; i++)
			{
				Variant metadata = opt.GetItemMetadata(i);
				if (metadata.Obj != null && metadata.ToString() == selectedTag)
				{
					opt.Select(i);
					
					if (opt.HasMeta("component_id"))
					{
						int metaId = (int)opt.GetMeta("component_id");
						var comp = SceneComponents.GetComponentById(metaId, currentScene);
						if (comp != null) comp.Tag = selectedTag;
					}
					break;
				}
			}
		}
	}

	private bool OptionButtonContains(OptionButton opt, string tag)
	{
		if (opt == null) return false;

		for (int i = 0; i < opt.ItemCount; i++)
			if (opt.GetItemText(i) == tag)
				return true;
		return false;
	}
}