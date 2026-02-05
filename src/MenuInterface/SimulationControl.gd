extends PanelContainer

@onready var play_button: Button = $HBoxContainer/Play
@onready var pause_button: Button = $HBoxContainer/Pause
@onready var stop_button: Button = $HBoxContainer/Stop
@onready var inputs_button: Button = $HBoxContainer/Inputs
@onready var outputs_button: Button = $HBoxContainer/Outputs
@onready var menu_button: MenuButton = $MenuButton
@onready var cena_config_menu = $"../CommsConfigMenu"
@onready var cena_runbar = $"."

var root_node = null

var play = false
var pause = true
var stop = true
var input = true
var output = true

func _disable_buttons() -> void:
	play_button.disabled = true
	pause_button.disabled = true
	stop_button.disabled = true
	
func _enable_buttons() -> void:
	play_button.disabled = play
	pause_button.disabled = pause
	stop_button.disabled = stop


func _on_ready() -> void:
	print("\n> [SimulationControl.gd] [_on_ready]")
	get_tree().paused = false
	
	root_node = get_tree().current_scene
	
	GlobalVariables.simulation_started.connect(func () -> void:
		play_button.disabled = true
		pause_button.disabled = false
		stop_button.disabled = false
		play = true
		pause = false
		stop = false
	)
	GlobalVariables.simulation_ended.connect(func () -> void:
		play_button.disabled = false
		pause_button.disabled = true
		stop_button.disabled = true
		play = false
		pause = true
		stop = true	
	)
	
	# Menu Popup
	menu_button.get_popup().add_item("Configurações")
	menu_button.get_popup().add_separator()
	menu_button.get_popup().add_item("Restaurar Configurações")
	menu_button.get_popup().add_separator()
	menu_button.get_popup().add_item("Menu Inicial")
	menu_button.get_popup().add_separator()
	menu_button.get_popup().add_item("Sair")
	
	menu_button.get_popup().connect("id_pressed", _on_item_pressed)

# Função do MenuPopup
func _on_item_pressed(id):
	# Pega o texto de cada ID dentro do MenuPopup, a partir dos nomes fazemos
	# um match (switch case) para funcionamentos diferentes.
	var item_name = menu_button.get_popup().get_item_text(id)
	
	match item_name:
		"Configurações":
			cena_config_menu.visible = not cena_config_menu.visible
			cena_runbar.visible = not cena_runbar.visible
		"Restaurar Configurações":
			_restaurar_configuracoes()
		"Menu Inicial":
			get_tree().change_scene_to_file("res://src/MenuInicial/menu_inicial.tscn")
		"Sair":
			get_tree().quit()


func _restaurar_configuracoes() -> void:
	print("\n> [SimulationControl.gd] [_restaurar_configuracoes]")
	
	# Diálogo de confirmação
	var dialog = AcceptDialog.new()
	dialog.dialog_text = "Isso irá restaurar TODAS as configurações da cena para os valores originais.\n\nTags OPC NÃO serão afetadas.\n\nTem certeza?"
	dialog.title = "Confirmar Restauração"
	dialog.min_size = Vector2(400, 150)
	
	# Conecta evento de confirmação
	dialog.confirmed.connect(func():
		# Chama método C# do Root para restaurar
		if root_node != null and root_node.has_method("RestoreSceneConfigs"):
			root_node.RestoreSceneConfigs()
			
			var success_dialog = AcceptDialog.new()
			success_dialog.dialog_text = "Configurações restauradas com sucesso!\n\nAs mudanças serão aplicadas na próxima vez que a simulação iniciar."
			success_dialog.title = "Sucesso"
			add_child(success_dialog)
			success_dialog.popup_centered()
			
			print("[SimulationControl] Configurações restauradas.")
		else:
			push_error("[SimulationControl] Root não possui método 'RestoreSceneConfigs'")
	)
	
	add_child(dialog)
	dialog.popup_centered()


func on_play_pressed() -> void:
		print("\n> [SimulationControl.gd] [on_play_pressed]")
		
		get_tree().current_scene.SavePositions();
		
		pause_button.button_pressed = false
		play = false
		GlobalVariables.simulation_set_paused.emit(false)
		GlobalVariables.simulation_started.emit()
		#if(EditorInterface.has_method("set_simulation_started")):
			#EditorInterface.call("set_simulation_started",true)


func _on_pause_pressed() -> void:
		print("\n> [SimulationControl.gd] [_on_pause_pressed]")
		GlobalVariables.simulation_set_paused.emit(pause_button.button_pressed)


func _on_stop_pressed() -> void:
		print("\n> [SimulationControl.gd] [_on_stop_pressed]")
		
		get_tree().current_scene.ResetPositions();
		
		pause_button.button_pressed = false
		pause = false
		GlobalVariables.simulation_set_paused.emit(false)
		GlobalVariables.simulation_ended.emit()
		#if(EditorInterface.has_method("set_simulation_started")):
			#EditorInterface.call("set_simulation_started",false)


func _on_inputs_pressed() -> void:
	pass # Replace with function body.


func _on_outputs_pressed() -> void:
	pass # Replace with function body.
