# DualWieldPrototype

Sauberer Neustart fuer das Dual-Wield-Prototyping in Bannerlord 1.3.

Aktueller Fokus:

- zweite 1h-Waffe links anheften
- `RMB` startet nur noch einen groben linken Proxy-Angriff ueber `act_quick_release_swingleft_fist_left_stance`
- optionaler `Unarmed Trace Mode`, um nativen Faustkampf ohne Prototyp-Eingriff zu analysieren
- optionales `Trace Native Channel Calls`, um `Agent.SetActionChannel` fuer den Main-Agent mitzuschneiden

MCM:

- `Enable Prototype`
- `Live Messages`
- `Debug File Logging`
- `Deep Action Logging`
- `Trace Native Channel Calls`
- `Unarmed Trace Mode`
- `Fist Compare Mode`
- `Proxy Attack Action`
- `Proxy Cooldown`
- `Gate Slash Proxy To Left Stance`
- `Rotation Preset`
- `Offset X/Y/Z`

Verhalten:

- normaler Testmodus:
- `LMB` bleibt Vanilla-Mainhand
- `RMB` blockt nicht mehr, sondern startet einmal pro Klick den aktuell gewaehlten Proxy-Angriff
- `Fist Compare Mode = true`:
- `LMB` startet `act_quick_release_swingright_fist`
- `RMB` startet `act_quick_release_swingleft_fist_left_stance`
- beide laufen weiter mit angehefteter Offhand-Waffe und sind nur fuer Diagnosen gedacht
- `Proxy Attack Action`:
- `LeftFistSwing`: `act_quick_release_swingleft_fist_left_stance`
- `SlashLeft1hLeftStance`: `act_quick_release_slashleft_1h_left_stance`
- `Gate Slash Proxy To Left Stance`: wartet fuer `SlashLeft1hLeftStance` auf ein echtes `leftStance=true`, bevor der Proxy gestartet wird
- die Offhand-Waffe bleibt links angeheftet, damit der Proxy wie ein grober linker Waffenangriff wirkt

- `Unarmed Trace Mode = true`:
- keine Dual-Wield-Steuerung
- keine angeheftete Offhand
- nur nativer Faustkampf plus Logging

Logging:

- Logdatei: `Modules/DualWieldPrototype/dualwieldprototype.log`
- wichtige Zeilen:
- `settings_applied ...`
- `loadout ...`
- `attach ...`
- `attack_request ...`
- `attack_started ...`
- `attack_diag ...`
- `trace_setaction ...`
- `unarmed_trace_state ...`
- `leftstance_change ...`

Dieser Stand ist bewusst klein gehalten. Alte Legacy-/Cycle-/Follow-up-Experimente wurden entfernt, damit neue Tests auf einem klaren Basispunkt starten.

XML-Diagnosetest:

- `dwp_xml_mainhand`
- `dwp_xml_offhand`
- `dwp_fist_style_1h`
- `dwp_fist_style_offhand`
- `ModuleData/project.mbproj` registriert `item_usage_sets.xml`
- `SubModule.xml` laedt nur die Test-Items

Ziel des XML-Tests:

- ein 1h-Usage-Set im Stil der Faust-Usages pruefen
- insbesondere die `require_free_left_hand`- und `is_left_stance`-Kombinationen
- ohne zu behaupten, dass beliebige Vanilla-/Fremditems zur Laufzeit auf ein anderes `item_usage` umgebogen werden koennen
