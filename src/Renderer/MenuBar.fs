module MenuBar

open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Fable.Import.Electron
open Node.Exports
open Node.Base
open Update
open Settings
open Tabs

let handlerCaster f = System.Func<MenuItem, BrowserWindow, unit> f |> Some

let menuSeparator = 
    let sep = createEmpty<MenuItemOptions>
    sep.``type`` <- Some Separator
    sep

let makeItem (label:string) (iKeyOpt: string option)  (iAction: unit -> unit) =
    let item = createEmpty<MenuItemOptions>
    item.label <- Some label
    item.accelerator <- iKeyOpt
    item.click <- handlerCaster (fun _ _ -> iAction())
    item
    
let makeRoleItem label accelerator role = 
    let item = makeItem label accelerator id
    item.role <- U2.Case1 role |> Some
    item

let makeMenu (name:string) (table:MenuItemOptions list) =
    let subMenu = createEmpty<MenuItemOptions>
    subMenu.label <- Some name
    subMenu.submenu <-
        table
        |> ResizeArray<MenuItemOptions>
        |> U2.Case2 |> Some
    subMenu

let fileMenu =
    makeMenu "File" [
            makeItem "New"      (Some "CmdOrCtrl+N")        createFileTab
            menuSeparator
            makeItem "Save"     (Some "CmdOrCtrl+S")        saveFile
            makeItem "Save As"  (Some "CmdOrCtrl+Shift+S")  saveFileAs
            makeItem "Open"     (Some "CmdOrCtrl+O")        openFile
            menuSeparator
            makeItem "Close"    (Some "Ctrl+W")             deleteCurrentTab
            menuSeparator
            makeItem "Quit"     (Some "Ctrl+Q")         electron.remote.app.quit
        ]

let editMenu = 
    makeMenu "Edit" [
        makeItem "Undo" (Some "CmdOrCtrl+Z") editorUndo
        makeItem "Redo" (Some "CmdOrCtrl+Shift+Z") editorRedo
        menuSeparator
        makeRoleItem "Cut" (Some "CmdOrCtrl+X") MenuItemRole.Cut
        makeRoleItem "Copy" (Some "CmdOrCtrl+C") MenuItemRole.Copy   
        makeRoleItem "Paste" (Some "CmdOrCtrl+V")  MenuItemRole.Paste
        menuSeparator
        makeItem "Select All"  (Some "CmdOrCtrl+A")  editorSelectAll
        menuSeparator
        makeItem "Find" (Some "CmdOrCtrl+F") editorFind              
        makeItem "Replace"  (Some "CmdOrCtrl+H") editorFindReplace
        menuSeparator
        makeItem  "Preferences"  Option.None createSettingsTab
    ]


let viewMenu = 
        let devToolsKey = if Node.Globals.``process``.platform = NodeJS.Platform.Darwin then "Alt+Command+I" else "Ctrl+Shift+I"
        makeMenu "View" [
            makeRoleItem "Toggle Fullscreen"  (Some "F11")  MenuItemRole.Togglefullscreen
            menuSeparator
            makeRoleItem  "Zoom In" (Some "CmdOrCtrl+Plus") MenuItemRole.Zoomin 
            makeRoleItem  "Zoom Out"  (Some "CmdOrCtrl+-") MenuItemRole.Zoomout 
            makeRoleItem  "Reset Zoom"  (Some "CmdOrCtrl+0") MenuItemRole.Resetzoom
            menuSeparator
            makeItem "Toggle Dev Tools" (Some "F12") (electron.remote.getCurrentWebContents()).toggleDevTools
        ]

let helpMenu =
        let runPage page () = electron.shell.openExternal page |> ignore ; ()
        makeMenu "Help" [
            makeItem "Website" Core.Option.None (runPage "https://intranet.ee.ic.ac.uk/t.clarke/hlp/")
            makeItem "ARM documentation" Core.Option.None (runPage "http://infocenter.arm.com/help/index.jsp?topic=/com.arm.doc.ddi0234b/i1010871.html")
            makeItem "About" Core.option.None ( fun () -> 
                electron.remote.dialog.showMessageBox (
                      let opts = createEmpty<ShowMessageBoxOptions>
                      opts.title <- "Visual2 ARM Simulator (prerelease)" |> Some
                      opts.message <- "(c) 2018, Imperial College" |> Some
                      opts.detail <- 
                            "Acknowledgements: Salman Arif (VisUAL), HLP 2018 class" +
                            " (F# reimplementation), with special mention to Thomas Carrotti," +
                            " Lorenzo Silvestri, and HLP Team 10" |> Some
                      opts
                ) |> ignore; () )
         ]   

let setMainMenu () =
    let template = 
        ResizeArray<MenuItemOptions> [
            fileMenu
            editMenu
            viewMenu
            helpMenu
        ]
    template
    |> electron.remote.Menu.buildFromTemplate
    |> electron.remote.Menu.setApplicationMenu