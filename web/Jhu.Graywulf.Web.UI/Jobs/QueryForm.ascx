﻿<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="QueryForm.ascx.cs" Inherits="Jhu.Graywulf.Web.UI.Jobs.QueryForm" %>
<div class="dock-bottom">
    <p class="FormMessage"></p>
    <p class="FormButtons">
        <asp:Button runat="Server" ID="Edit" Text="Edit query" CssClass="FormButton" OnClick="Edit_Click" />
    </p>
</div>
<div class="dock-fill" style="border: 1px solid #000000" id="EditorDiv">
    <jgwc:CodeMirror runat="server" ID="Query" Mode="text/x-sql" Theme="default" />
</div>
