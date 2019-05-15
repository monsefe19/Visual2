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
open Update
open Tooltips2
open Files2

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
        Settings = 
            {
                EditorFontSize = "16"
                SimulatorMaxSteps = "20000"
                EditorTheme = "solarised-dark"
                EditorWordWrap = "off"
                EditorRenderWhitespace = "none"
                CurrentFilePath = Fable.Import.Node.Exports.os.homedir()
                RegisteredKey = ""
                OnlineFetchText = ""
            }
        DialogBox = None
    }, Cmd.none

let update (msg : Msg) (m : Model) =
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
            let newEditors = editorTextChangeUpdate str 
                                                    m.CurrentFileTabId 
                                                    m.Editors 
            { m with Editors = newEditors }
        | NewFile -> 
            let newTabId = uniqueTabId m.Editors
            let newEditors = Map.add newTabId blankTab m.Editors
            { m with CurrentFileTabId = newTabId
                     Editors = newEditors }
        | SelectFileTab id -> 
            let newTabId = selectFileTabUpdate id m.Editors
            { m with CurrentFileTabId = newTabId }
        | DeleteTab id -> 
            let newTabId, newEditors = 
                deleteTabUpdate id 
                                m.CurrentFileTabId 
                                m.Editors
            { m with CurrentFileTabId = newTabId
                     Editors = newEditors }
        | OpenFile editors -> 
            let newEditors, newFilePath, newTabId = 
                openFileUpdate (m.Editors, editors)
                               m.Settings.CurrentFilePath
                               m.CurrentFileTabId
            { m with Editors = newEditors
                     CurrentFileTabId = newTabId
                     Settings = { m.Settings with CurrentFilePath = filePathSetting newFilePath }
                     DialogBox = None }
        | OpenFileDialog -> 
             { m with DialogBox = Some OpenFileDl }
        | SaveFile -> 
            match m.CurrentFileTabId with
            | -1 -> 
                m
            | x -> 
                let filePath = m.Editors.[x].FilePath
                match filePath with
                | None -> { m with DialogBox = Some SaveAsDl }
                | Some y -> 
                    let currentEditor = m.Editors.[x]
                    writeToFile currentEditor.EditorText
                                y
                    let newEditors = Map.add m.CurrentFileTabId 
                                             { currentEditor with Saved = true }
                                             m.Editors
                    { m with Editors = newEditors }
        | SaveAsFileDialog -> 
            match m.CurrentFileTabId with
            | -1 -> m
            | _ -> { m with DialogBox = Some SaveAsDl }
        | SaveAsFile fileInfo ->
            match fileInfo with
            | None -> { m with DialogBox = None }
            | Some (filePath, fileName) ->
                let newEditor =
                    { m.Editors.[m.CurrentFileTabId] with FilePath = Some filePath
                                                          FileName = Some fileName
                                                          Saved = true }
                let newEditors = 
                    m.Editors
                    |> Map.add m.CurrentFileTabId
                               newEditor
                    |> Map.filter (fun key value -> 
                        key = m.CurrentFileTabId ||
                        value.FilePath <> newEditor.FilePath )
                let newSettings = 
                    { m.Settings with CurrentFilePath = filePathSetting filePath }
                { m with Editors = newEditors 
                         DialogBox = None 
                         Settings = newSettings }
    model, Cmd.none

let view (m : Model) (dispatch : Msg -> unit) =
    mainMenu dispatch
    dialogBox m dispatch
    Browser.console.log(string m.Editors)
    Browser.console.log(string m.CurrentFileTabId)
    Browser.console.log(string m.Settings.CurrentFilePath)
    div [ ClassName "window" ] 
        [ header [ ClassName "toolbar toolbar-header" ] 
                 [ div [ ClassName "toolbar-actions" ] 
                       [ div [ ClassName "btn-group" ]
                             [ button [ ClassName "btn btn-default" 
                                        DOMAttr.OnClick (fun _ -> OpenFileDialog |> dispatch) ]
                                      [ span [ ClassName "icon icon-folder" ] [] ]
                               button [ ClassName "btn btn-default"
                                        DOMAttr.OnClick (fun _ -> SaveFile |> dispatch) ]
                                      [ span [ ClassName "icon icon-floppy" ] [] ]]
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
                                                 [ str "-" ]]]
                         repButtons m.CurrentRep dispatch ]]
          div [ ClassName "window-content" ] 
              [ div [ ClassName "pane-group" ] 
                    [ div [ ClassName "pane file-view-pane"] 
                          (editorPanel m.CurrentFileTabId m.Editors dispatch)
                      div [ ClassName "pane dashboard"
                            dashboardStyle m.CurrentRep ]
                          [ viewButtons m.CurrentView dispatch
                            viewPanel m dispatch
                            footer m.Flags ]]]]

Program.mkProgram init update view
#if DEBUG
|> Program.withHMR
#endif
|> Program.withReact "app"
|> Program.run