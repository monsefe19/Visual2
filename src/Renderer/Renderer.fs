module Renderer

open Refs
open Elmish
open Elmish.React
open Elmish.HMR
open Elmish.Debug
open Elmish.Browser.Navigation
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Fable.Import.Browser
open Fable.Import.React
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Monaco
open Monaco.Monaco.Languages
open Monaco.Monaco
open Views2
open Tabs2
open MenuBar2
open Tooltips2
open Files2
open Settings2
open Editors2
open Dialog

let init _ =
    { 
        CurrentFileTabId = 0
        TestbenchTab = None
        Editors = Map.ofList [ (0, blankTab)]
        CurrentTabWidgets = Map.empty
        SettingsTab = None
        CurrentRep = Hex
        DisplayedCurrentRep = Hex
        CurrentView = Registers
        ByteView = false
        ReverseDirection = false
        MaxStepsToRun = 50000
        MemoryMap = Map.empty
        RegMap = ExecutionTop.initialRegMap
        Flags = initialFlags
        SymbolMap = Map.empty
        DisplayedSymbolMap = Map.empty
        RunMode = ExecutionTop.ResetMode
        DebugLevel = 0
        LastOnlineFetchTime = Result.Error System.DateTime.Now
        Activity = true
        Sleeping = false
        LastRemindTime = None
        Settings = getJSONSettings()
        DialogBox = None
    }, Cmd.none

let update (msg : Msg) (m : Model) =
    let mutable cmd = Cmd.none
    let model = 
        match msg with
        | ChangeView view -> 
            { m with CurrentView = view }
        | ChangeRep rep ->
            { m with CurrentRep = rep }
        | ToggleByteView -> 
            { m with ByteView = not m.ByteView }
        | ToggleReverseView -> 
            { m with ReverseDirection = not m.ReverseDirection }
        | EditorTextChange str -> 
            let newEditors = 
                editorTextChangeUpdate (m.CurrentFileTabId, m.Editors) 
                                       str    
            { m with Editors = newEditors }
        | NewFile -> 
            let newTabId, newEditors = newFileUpdate m.Editors
            { m with CurrentFileTabId = newTabId
                     Editors = newEditors }
        | SelectFileTab id -> 
            let newTabId = selectFileTabUpdate id m.Editors
            { m with CurrentFileTabId = newTabId }
        | AttemptToDeleteTab id ->
            let newDialog, newCmd =
                attemptToDeleteTabUpdate (m.CurrentFileTabId, m.Editors.[m.CurrentFileTabId].Saved, m.DialogBox) 
                                         id
            cmd <- newCmd
            { m with DialogBox = newDialog }
        | DeleteTab -> 
            let newTabId, newEditors, newSettingsTab = 
                deleteTabUpdate (m.CurrentFileTabId, m.Editors, m.SettingsTab)
            { m with CurrentFileTabId = newTabId
                     Editors = newEditors 
                     SettingsTab = newSettingsTab 
                     DialogBox = None }
        | OpenFile editors -> 
            let newEditors, newFilePath, newTabId = 
                openFileUpdate (m.Editors, m.Settings.CurrentFilePath, m.CurrentFileTabId)
                               editors
            let newSettings = 
                { m.Settings with CurrentFilePath = newFilePath }
            { m with Editors = newEditors
                     CurrentFileTabId = newTabId
                     Settings = newSettings
                     DialogBox = None }
        | OpenFileDialog -> 
            let newDialog = openFileDialogUpdate m.DialogBox
            { m with DialogBox = newDialog }
        | SaveFile -> 
            let newDialog, newEditors = 
                saveFileUpdate (m.CurrentFileTabId, m.Editors)
            { m with Editors = newEditors
                     DialogBox = newDialog }
        | SaveAsFileDialog -> 
            let newDialogBox =
                saveAsFileDialogUpdate (m.CurrentFileTabId, m.DialogBox)
            { m with DialogBox = newDialogBox }
        | SaveAsFile fileInfo ->
            let newEditors, newFilePathSetting =
                saveAsFileUpdate (m.Editors, m.CurrentFileTabId, m.Settings.CurrentFilePath)
                                 fileInfo
            let newSettings = 
                { m.Settings with CurrentFilePath = newFilePathSetting }
            { m with DialogBox = None 
                     Editors = newEditors
                     Settings = newSettings }
        | SelectSettingsTab ->
            let newEditors, newTabId =
                selectSettingsTabUpdate (m.SettingsTab, m.Editors)
            { m with Editors = newEditors
                     CurrentFileTabId = newTabId
                     SettingsTab = Some newTabId }
        | SaveSettings ->
            let newSettings, newEditors, newId =
                saveSettingsUpdate (m.Settings, m.Editors, m.SettingsTab.Value)
            { m with Settings = newSettings 
                     Editors = newEditors 
                     CurrentFileTabId = newId 
                     SettingsTab = None }
        | LoadDemoCode -> 
            let newEditors, newId = loadDemo m.Editors
            { m with Editors = newEditors 
                     CurrentFileTabId = newId }
        | IncreaseFontSize ->
            let newSettings = { m.Settings with EditorFontSize = string ((int m.Settings.EditorFontSize) + 2) }
            { m with Settings = newSettings }
        | DecreaseFontSize ->
            let newSettings = { m.Settings with EditorFontSize = string ((int m.Settings.EditorFontSize) - 2) }
            { m with Settings = newSettings }
        | AboutDialog ->
            let newDialog = aboutDialogUpdate m.DialogBox
            { m with DialogBox = newDialog }
        | CloseDialog ->
            { m with DialogBox = None }
        | AttemptToExit ->
            let newDialogBox, newCmd = attemptToExitUpdate m.Editors m.DialogBox
            cmd <- newCmd
            { m with DialogBox = newDialogBox }
        | Exit ->
            close()
            { m with DialogBox = None }
    model, cmd

let view (m : Model) (dispatch : Msg -> unit) =
    mainMenu m.CurrentFileTabId dispatch m.Editors
    dialogBox (m.DialogBox, m.Settings.CurrentFilePath, m.Editors, m.CurrentFileTabId)
              dispatch
    Browser.console.log(string m.Editors)
    Browser.console.log(string m.CurrentFileTabId)
    div [ ClassName "window" ] 
        [ header [ ClassName "toolbar toolbar-header" ] 
                 [ div [ ClassName "toolbar-actions" ] 
                       [ div [ ClassName "btn-group" ]
                             [ button [ ClassName "btn btn-default" 
                                        DOMAttr.OnClick (fun _ -> OpenFileDialog |> dispatch) ]
                                      [ span [ ClassName "icon icon-folder" ] [] ]
                               button [ ClassName "btn btn-default"
                                        DOMAttr.OnClick (fun _ -> SaveFile |> dispatch) ]
                                      [ span [ ClassName "icon icon-floppy" ] [] ] ]
                         button [ ClassName "btn btn-fixed btn-default button-run" ]
                                [ str "Run" ]
                         button [ ClassName "btn btn-default" ]
                                [ str "Reset" ]
                         button [ ClassName "btn btn-default button-back" ]
                                [ str " Step" ]
                         button [ ClassName "btn btn-default button-forward" ]
                                [ str "Step " ]
                         button [ ClassName "btn btn-large btn-default status-bar" ]//; Disabled true ]
                                [ str "-" ]
                         div [ ClassName "btn-group clock" ]
                             [ tooltips (Content clockSymTooltipStr :: Placement "bottom" :: basicTooltipsPropsLst)
                                        [ button [ ClassName "btn btn-large btn-default clock-symbol" ]
                                                 [ str "\U0001F551" ]]
                               tooltips (Content clockTooltipStr :: Placement "bottom" :: basicTooltipsPropsLst)
                                        [ button [ ClassName "btn btn-large btn-default clock-time" ]//; Disabled true ]
                                                 [ str "-" ] ] ]
                         repButtons m.CurrentRep dispatch ] ]
          div [ ClassName "window-content" ] 
              [ div [ ClassName "pane-group" ] 
                    [ div [ ClassName "pane file-view-pane"] 
                          (editorPanel (m.CurrentFileTabId, m.Editors, m.SettingsTab, m.Settings) 
                                       dispatch)
                      div [ ClassName "pane dashboard"
                            dashboardStyle m.CurrentRep ]
                          [ viewButtons m.CurrentView dispatch
                            viewPanel (m.CurrentRep, m.CurrentView, m.RegMap) 
                                      (m.MemoryMap, m.SymbolMap, m.ByteView, m.ReverseDirection)
                                      dispatch
                            footer m.Flags ] ] ] ]

Program.mkProgram init update view
#if DEBUG
|> Program.withHMR
#endif
|> Program.withReact "app"
|> Program.run