(* 
    High Level Programming @ Imperial College London # Spring 2018
    Project: A user-friendly ARM emulator in F# and Web Technologies ( Github Electron & Fable Compliler )
    Contributors: Angelos Filos
    Module: Main
    Description: Electron Main Process.
*)

module Main

open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Fable.Import.Electron
open Node.Exports

// Keep a global reference of the window object, if you don't, the window will
// be closed automatically when the JavaScript object is garbage collected.
let mutable mainWindow: BrowserWindow option = Option.None

// Used for right-click context menu
[<Emit("require('electron-context-menu')({});")>]
let contextMenu () = jsNative

let createMainWindow () =
    contextMenu()
    let options = createEmpty<BrowserWindowOptions>
    // Complete list of window options
    // https://electronjs.org/docs/api/browser-window#new-browserwindowoptions
    options.width <- Some 1200.
    options.height <- Some 800.
    options.frame <- Some true
    let window = electron.BrowserWindow.Create(options)

    // Load the index.html of the app.
    let opts = createEmpty<Node.Url.Url<obj>>
    opts.pathname <- Some <| path.join(Node.Globals.__dirname, "/app/index.html")
    opts.protocol <- Some "file:"
    window.loadURL(url.format(opts))
    window.webContents.openDevTools()

    #if DEBUG
    fs.watch(path.join(Node.Globals.__dirname, "/app/js/renderer.js"), fun _ _ ->
        window.webContents.reloadIgnoringCache()
    ) |> ignore
    #endif

    // Emitted when the window is closed.
    window.on("closed", unbox(fun () ->
        // On closing of the main window, close the whole application
        electron.app.quit()
    )) |> ignore

    // Maximize the window
    window.maximize()

    // Clear the menuBar, this is overwritten by the renderer process
    let template = ResizeArray<MenuItemOptions> [
                        createEmpty<MenuItemOptions>
                    ]
    electron.Menu.setApplicationMenu(electron.Menu.buildFromTemplate(template))

    mainWindow <- Some window

    
    
// This method will be called when Electron has finished
// initialization and is ready to create browser windows.
electron.app.on("ready", unbox createMainWindow ) |> ignore

// Quit when all windows are closed.
electron.app.on("window-all-closed", unbox(fun () ->
    // On OS X it is common for applications and their menu bar
    // to stay active until the user quits explicitly with Cmd + Q
    if Node.Globals.``process``.platform <> Node.Base.NodeJS.Darwin then
        electron.app.quit()
)) |> ignore

electron.app.on("activate", unbox(fun () ->
    // On OS X it's common to re-create a window in the app when the
    // dock icon is clicked and there are no other windows open.
    if mainWindow.IsNone then
        createMainWindow()
    printfn "Hello from main process..."
)) |> ignore