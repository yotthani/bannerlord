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
- `Left Proxy Compare Mode`
- `Left 1H Slash Compare Mode`
- `Left 1H Compare Channel`
- `Proxy Attack Action`
- `Proxy Cooldown`
- `Offhand Wield Probe`
- `Gate Slash Proxy To Left Stance`
- `Prime Slash With Left Flags`
- `Slash Anim Flag Mode`
- `Slash Transition Mode`
- `Slash Flow Mode`
- `Fist Then Slash Delay`
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
- `Left Proxy Compare Mode = true`:
- `LMB` startet `act_quick_release_swingleft_fist_left_stance`
- `RMB` startet `act_quick_release_slashleft_1h_left_stance`
- `V` startet erst `act_quick_release_swingleft_fist_left_stance` und legt kurz danach `act_quick_release_slashleft_1h_left_stance` auf `ch0`, um zu testen, ob der funktionierende LeftFist-State `SlashL` mitzieht
- `Fist Then Slash Delay`: steuert genau dieses kurze Fenster fuer den `V`-Vergleich
- beide laufen ueber denselben direkten Proxy-Pfad und unterscheiden sich nur in der Action-ID
- `Left 1H Slash Compare Mode = true`:
- `LMB` startet `act_ready_slashleft_1h_left_stance -> act_release_slashleft_1h_left_stance`
- `RMB` startet `act_ready_slashright_1h_left_stance -> act_release_slashright_1h_left_stance`
- beide nutzen denselben Ready/Release-Testpfad, um zu pruefen, ob bei 1h-left-stance wie bei den Fausten schon der Ready-State die semantische Richtung festlegt
- `Left 1H Compare Channel`:
- `Channel0`: aktueller Proxy-Pfad
- `Channel1`: gleicher 1h-left-ready/release-Test auf dem Kanal, auf dem native linke Faustangriffe laufen
- `Proxy Attack Action`:
- `LeftFistSwing`: `act_quick_release_swingleft_fist_left_stance`
- `SlashLeft1hLeftStance`: `act_quick_release_slashleft_1h_left_stance`
- `RotDualSlashLeft1hLeftStance`: `act_dual_quick_release_slashleft_1h_left_stance`
- `RotDualThrust1hLeftStance`: `act_dual_quick_release_thrust_1h_left_stance`
- `Gate Slash Proxy To Left Stance`: wartet fuer `SlashLeft1hLeftStance` auf ein echtes `leftStance=true`, bevor der Proxy gestartet wird
- `Prime Slash With Left Flags`: setzt direkt vor `slashleft_1h_left_stance` testweise einen linken Attack-MovementFlag, um den fehlenden Combat-State gegen den funktionierenden LeftFist-Pfad zu vergleichen
- `Slash Anim Flag Mode`: A/B-Test fuer `slashleft_1h_left_stance`
- `None`: keine zusaetzlichen `AnimFlags`
- `UseLeftHandDuringAttack`: setzt `AnimFlags.anf_use_left_hand_during_attack`
- `Slash Transition Mode`: A/B-Test fuer denselben Slash-Pfad
- `Default`: normaler `SetActionChannel`-Blendpfad
- `Instant`: harter Uebergang mit `blendIn=0` und `blendOutToNoAnim=0`
- `Slash Flow Mode`: A/B-Test fuer den eigentlichen Angriffspfad
- `DirectQuickRelease`: aktueller Direktstart des Release-Clips
- `ReadyThenRelease`: startet zuerst die passende `ready_action` und feuert `release_action` beim Loslassen oder per Timeout-Fallback
- `Offhand Wield Probe`: diagnostischer Native-Wield-Versuch fuer den aktuellen Offhand-Slot
- `Disabled`: nur Attach-to-bone
- `Instant` / `InstantAfterPickUp` / `WithAnimation`: ruft `TryToWieldWeaponInSlot(offhandSlot, ...)` auf und loggt den resultierenden Offhand-State
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
- `wield_state ...`
- `offhand_probe_begin ...`
- `offhand_probe_end ...`

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
