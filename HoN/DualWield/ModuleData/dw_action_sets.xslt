<?xml version="1.0" encoding="utf-8"?>
<!--
  DualWield Mod - Patches dual-wield actions into as_human_warrior.
  Based on ROT 8.0's action_sets.xslt structure.

  Right-hand (normal) actions: map to vanilla 1h animation clips.
  Left-hand (_left_stance) actions: map to fist_left_stance clips.

  Only swingleft and uppercut fist_left_stance actually animate the left arm.
  swingright and direct still animate the right arm (engine limitation).
  For now, map all LH directions to the 2 working clips:
    - slash/swing directions → swingleft_fist_left_stance
    - thrust/overswing directions → uppercut_fist_left_stance
-->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output omit-xml-declaration="yes"/>

  <!-- Identity copy -->
  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Inject dual-wield actions into as_human_warrior -->
  <xsl:template match="action_set[@id='as_human_warrior']">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()"/>

      <!-- ==================== THRUST ==================== -->
      <!-- RH: vanilla 1h thrust animations -->
      <action name="act_dw_ready_thrust_1h" animation="ready_thrust_1h"/>
      <action name="act_dw_quick_release_thrust_1h" animation="quick_release_thrust_1h"/>
      <action name="act_dw_release_thrust_1h" animation="release_thrust_1h"/>
      <action name="act_dw_quick_blocked_thrust_1h" animation="quick_blocked_thrust_1h"/>
      <action name="act_dw_blocked_thrust_1h" animation="blocked_thrust_1h"/>
      <!-- LH: fist uppercut (vertical upward = closest to thrust) -->
      <action name="act_dw_ready_thrust_1h_left_stance" animation="ready_uppercut_fist_left_stance"/>
      <action name="act_dw_quick_release_thrust_1h_left_stance" animation="release_uppercut_fist_left_stance"/>
      <action name="act_dw_release_thrust_1h_left_stance" animation="release_uppercut_fist_left_stance"/>
      <action name="act_dw_quick_blocked_thrust_1h_left_stance" animation="ready_uppercut_fist_left_stance"/>
      <action name="act_dw_blocked_thrust_1h_left_stance" animation="ready_uppercut_fist_left_stance"/>

      <!-- ==================== SLASH RIGHT ==================== -->
      <!-- RH: vanilla 1h slash right animations -->
      <action name="act_dw_ready_slashright_1h" animation="ready_slashright_1h"/>
      <action name="act_dw_quick_release_slashright_1h" animation="quick_release_slashright_1h"/>
      <action name="act_dw_release_slashright_1h" animation="release_slashright_1h"/>
      <action name="act_dw_quick_blocked_slashright_1h" animation="quick_blocked_slashright_1h"/>
      <action name="act_dw_blocked_slashright_1h" animation="blocked_slashright_1h"/>
      <!-- LH: fist swingleft (horizontal sweep = closest to slash) -->
      <action name="act_dw_ready_slashright_1h_left_stance" animation="ready_swingleft_fist_left_stance"/>
      <action name="act_dw_quick_release_slashright_1h_left_stance" animation="release_swingleft_fist_left_stance"/>
      <action name="act_dw_release_slashright_1h_left_stance" animation="release_swingleft_fist_left_stance"/>
      <action name="act_dw_quick_blocked_slashright_1h_left_stance" animation="ready_swingleft_fist_left_stance"/>
      <action name="act_dw_blocked_slashright_1h_left_stance" animation="ready_swingleft_fist_left_stance"/>

      <!-- ==================== SLASH LEFT ==================== -->
      <!-- RH: vanilla 1h slash left animations -->
      <action name="act_dw_ready_slashleft_1h" animation="ready_slashleft_1h"/>
      <action name="act_dw_quick_release_slashleft_1h" animation="quick_release_slashleft_1h"/>
      <action name="act_dw_release_slashleft_1h" animation="release_slashleft_1h"/>
      <action name="act_dw_quick_blocked_slashleft_1h" animation="quick_blocked_slashleft_1h"/>
      <action name="act_dw_blocked_slashleft_1h" animation="blocked_slashleft_1h"/>
      <!-- LH: fist uppercut (alternate with swingleft for variety) -->
      <action name="act_dw_ready_slashleft_1h_left_stance" animation="ready_uppercut_fist_left_stance"/>
      <action name="act_dw_quick_release_slashleft_1h_left_stance" animation="release_uppercut_fist_left_stance"/>
      <action name="act_dw_release_slashleft_1h_left_stance" animation="release_uppercut_fist_left_stance"/>
      <action name="act_dw_quick_blocked_slashleft_1h_left_stance" animation="ready_uppercut_fist_left_stance"/>
      <action name="act_dw_blocked_slashleft_1h_left_stance" animation="ready_uppercut_fist_left_stance"/>

    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
