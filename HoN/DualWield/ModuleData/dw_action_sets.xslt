<?xml version="1.0" encoding="utf-8"?>
<!--
  DualWield Mod - Action Set Patch (IDENTITY)

  Unlike ROT which patches 76 custom action-to-animation mappings
  into as_human_warrior, we reuse vanilla action types directly.
  No custom animation clip mappings needed.

  This XSLT passes through unchanged. It can be extended later
  if we add custom dual wield animation clips.
-->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output omit-xml-declaration="yes"/>

  <!-- Identity copy - passes all content through unchanged -->
  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
