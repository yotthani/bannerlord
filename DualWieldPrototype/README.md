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
- `Proxy Cooldown`
- `Rotation Preset`
- `Offset X/Y/Z`

Verhalten:

- normaler Testmodus:
- `LMB` bleibt Vanilla-Mainhand
- `RMB` blockt nicht mehr, sondern startet einmal pro Klick den linken `FistProxy`
- die Offhand-Waffe bleibt links angeheftet, damit der Faustschlag wie ein grober linker Waffenangriff wirkt

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
