extends Control

@onready var menu_cenas = $HBoxContainer/PanelContainer/menu_cenas
@onready var menu_sobre = $HBoxContainer/PanelContainer/menu_sobre


func _ready() -> void:
	menu_cenas.visible = false
	menu_sobre.visible = false


func _on_cenas_pressed() -> void:
	menu_cenas.visible = true
	menu_sobre.visible = false


func _on_sobre_pressed() -> void:
	menu_cenas.visible = false
	menu_sobre.visible = true


# Quando clicar em SAIR
func _on_sair_pressed() -> void:
	get_tree().quit()


func _on_cena_1_pressed() -> void:
	get_tree().change_scene_to_file("res://src/scenes/scene_1/scene_1.tscn")


func _on_cena_2_pressed() -> void:
	get_tree().change_scene_to_file("res://src/scenes/scene_2/scene_2.tscn")


func _on_cena_3_pressed() -> void:
	get_tree().change_scene_to_file("res://src/scenes/scene_3/scene_3.tscn")


func _on_cena_4_pressed() -> void:
	get_tree().change_scene_to_file("res://src/scenes/scene_4/scene_4.tscn")


func _on_cena_5_pressed() -> void:
	get_tree().change_scene_to_file("res://src/scenes/scene_5/scene_5.tscn")
	
func _on_cena_6_pressed() -> void:
	get_tree().change_scene_to_file("res://src/scenes/scene_6/scene_6.tscn")
