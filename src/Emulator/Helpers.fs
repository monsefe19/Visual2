module Helpers
    open CommonData    
    open System.Text.RegularExpressions
    open CommonLex
    open Errors

    /// A function to check the validity of literals according to the ARM spec.
    (*let checkLiteral lit =
        let lst = [0..2..30] 
                    |> List.map (fun x -> rotRight lit x, x)
                    |> List.filter (fun (x, _) -> x < 256u)
        match lst with
        | [] ->
            let txt = lit |> string
            (txt, notValidLiteralEM)
            ||> makeError
            |> ``Invalid literal``
            |> Error
        | (x, r) :: _ -> Ok (byte x, r)
    *)
    /// Visuals apparent minimum data address may be useful?
    let minAddress = 0x100u
    /// Word Length 4 bytes
    let word = 0x4u
    /// The Old Chris QuickPrint classic from tick 1
    let qp item = printfn "%A" item
    let qpl lst = List.map (qp) lst

    /// Partial Active pattern for regexes. 
    /// Returns the 1st () element in the group
    let (|ParseRegex|_|) (regex: string) (str: string) =
       let m = Regex("^" + regex + "[\\s]*" + "$").Match(str)
       if m.Success
       then Some (m.Groups.[1].Value)
       else None

    /// Partial Active pattern for regexes. 
    /// Returns the 1st and 2nd () elements in the group
    let (|ParseRegex2|_|) (regex: string) (str: string) =
       let m = Regex("^" + regex + "[\\s]*" + "$").Match(str)
       if m.Success
       then Some (m.Groups.[1].Value, m.Groups.[2].Value)
       else None

    /// makes a reg
    let makeReg r = regNames.[r]
    /// Takes a number and converts it into a reg string
    let makeRegFn = (string >> (+) "R") // needed elsewhere

    /// makes a RName from a number
    let makeRegFromNum r =
        r |> makeRegFn |> makeReg

    /// Check if reg is valid by seeing if key is in map
    let regValid r =
        Map.containsKey r regNames

    /// Check all regs in lst are valid
    let regsValid rLst = 
        rLst 
        |> List.fold (fun b r -> b && (regValid r)) true

    let uppercase (x: string) = x.ToUpper()

    /// Split an input string at the provided charater.
    /// Return as a string list.
    let splitAny (str: string) char =
        let nospace = str.Replace(" ", "")                                    
        nospace.Split([|char|])              
        |> Array.map uppercase    
        |> List.ofArray

    let checkValid2 opList =
        match opList with
        | h1 :: h2 :: _ when (regsValid [h1 ; h2]) -> true 
        | _ -> false


    /// A partially active pattern to check validity of a register passed as a string,
    /// and return an `RName` if it is valid.
    let (|RegMatch|_|) txt =
        match Map.tryFind txt regNames with
        | Some reg ->
            reg |> Ok |> Some
        | _ ->
            None

    /// A partially active pattern that returns an error if a register argument is not valid.
    let (|RegCheck|_|) txt =
        match Map.tryFind txt regNames with
        | Some reg ->
            reg |> Ok |> Some
        | _ ->
            (txt, notValidRegEM)
            ||> makePE ``Invalid register``
            |> Some

//********************************************************************************
//
//                          EXECUTION HELPERS
//
//********************************************************************************

    /// Function for setting a register
    /// Takes RName and value and returns
    /// new DataPath with that register set.
    let setReg reg contents cpuData =
        let setter reg' old = 
            match reg' with
            | x when x = reg -> contents
            | _ -> old
        {cpuData with Regs = Map.map setter cpuData.Regs}
    
    /// Recursive function for setting multiple registers
    /// Need to check that the lists provided are the same length
    let rec setMultRegs regLst contentsLst cpuData =
        match regLst, contentsLst with
        | rhead :: rtail, chead :: ctail when (List.length regLst = List.length contentsLst) ->
            let newCpuData = setReg rhead chead cpuData
            setMultRegs rtail ctail newCpuData 
        | [], [] -> cpuData
        | _ -> failwith "Lists given to setMultRegs function were of different sizes."
        

    let updatePC (cpuData: DataPath) : DataPath =
        let pc = cpuData.Regs.[R15]
        setReg R15 (pc + 4u) cpuData
    
    let getPC (cpuData: DataPath) =
        cpuData.Regs.[R15] - 8u

    let getMemLoc addr cpuData =
        match Map.containsKey addr cpuData.MM with
        | true ->  cpuData.MM.[addr]
        | false -> failwithf "Attempted lookup:addr=%A, MM=%A" addr cpuData.MM

    let locExists m cpuData = 
        Map.containsKey m cpuData.MM

    /// Tom's condExecute instruction as he made it first (don't reinvent the wheel)
    let condExecute (cond:Condition) (cpuData: DataPath) =
        let n, c, z, v = (cpuData.Fl.N, cpuData.Fl.C, cpuData.Fl.Z, cpuData.Fl.V)
        match cond with
        | Cal -> true
        | Cnv -> false
        | Ceq -> z
        | Cne -> (not z)
        | Chs -> c
        | Clo -> (not c)
        | Cmi -> n
        | Cpl -> (not n)
        | Cvs -> v
        | Cvc -> (not v)
        | Chi -> (c && not z)
        | Cls -> (not c || z)
        | Cge -> (n = v)
        | Clt -> (n <> v)
        | Cgt -> (not z && (n = v))
        | Cle -> (z || (n <> v))
    
    /// Return a new datapath with reg rX set to value
    let updateReg value rX dp =
        {dp with Regs = Map.add rX value dp.Regs}
        
    let validateWA addr =
        match addr % word with
        | 0u -> true
        | _ -> false
            

    // Update the whole word at addr with value in dp
    // let updateMem value (addr : uint32) dp =
    //     match addr % 4u with
    //     | 0u -> {dp with MM = Map.add (WA addr) value dp.MM}
    //     | _ -> failwithf "Trying to update memory at unaligned address"

    let updateMemData value (addr : uint32) (dp: DataPath) =
        match validateWA addr with
        | false -> 
            (addr, " Trying to update memory at unaligned address.")
            |> ``Run time error``
            |> Error
        | true ->
        match Map.tryFind (WA addr) dp.MM with
        | None
        | Some (Dat _) -> Ok {dp with MM = Map.add (WA addr) value dp.MM}
        | Some CodeSpace -> 
            (addr , " Updating a byte in instruction memory space.")
            |> ``Run time error``
            |> Error


    /// Return the next aligned address after addr
    let alignAddress addr = (addr / word) * word
        
    /// Update a single byte in memory (Little Endian)
    // let updateMemByte (value : byte) (addr : uint32) dp =
    //     let baseAddr = alignAddress (addr)
    //     let shft = (int ((addr % 4u)* 8u))
    //     let mask = 0xFFu <<< shft |> (~~~)
    //     let oldVal = 
    //         match Map.containsKey (WA baseAddr) dp.MM with
    //         | true -> dp.MM.[WA baseAddr]
    //         | false -> DataLoc 0u // Uninitialised memory is zeroed
    //     let newVal = 
    //         match oldVal with
    //         | DataLoc x -> (x &&& mask) ||| ((uint32 value) <<< shft)
    //         | _ -> failwithf "Updating byte at instruction address"
    //     updateMem (DataLoc newVal) baseAddr dp
    
    let updateMemByte (value : byte) (addr : uint32) (dp: DataPath) =
        let baseAddr = alignAddress addr
        let shft = (int ((addr % word)* 8u))
        let mask = 0xFFu <<< shft |> (~~~)
        let oldVal = 
            match Map.containsKey (WA baseAddr) dp.MM with
            | true -> dp.MM.[WA baseAddr]
            | false -> Dat 0u // Uninitialised memory is zeroed
        match oldVal with
        | Dat x -> 
            let newVal = (x &&& mask) ||| ((uint32 value) <<< shft)
            updateMemData (Dat newVal) baseAddr dp
        | CodeSpace -> 
            (addr , " Updating a byte in instruction memory space.")
            |> ``Run time error``
            |> Error

   /// LDRB to load the correct byte
    let getCorrectByte value addr = 
        let shift = 8u * (addr % word) |> int32
        ((0x000000FFu <<< shift) &&& value) >>> shift

    let fetchMemData reg addr (cpuData: DataPath) =
        match validateWA addr with
        | true ->
            match addr with
            | a when (a < minAddress) ->
                (a, " Trying to access memory where instructions are stored.")
                |> ``Run time error``
                |> Error
            | _ -> 
                let baseAddr = alignAddress addr
                let wordAddr = WA baseAddr
                match locExists wordAddr cpuData with
                | true -> 
                    match getMemLoc wordAddr cpuData with
                    | Dat dl ->
                        setReg reg dl cpuData |> Ok
                    | CodeSpace -> 
                        (addr, " Trying to access memory where instructions are stored.")
                        |> ``Run time error``
                        |> Error
                | false -> setReg reg 0u cpuData |> Ok
        | false -> 
            (addr, " Trying to update memory at unaligned address.")
            |> ``Run time error``
            |> Error
    
    let fetchMemByte reg addr (cpuData: DataPath) =
        let baseAddr = alignAddress addr
        let wordAddr = WA baseAddr
        match locExists wordAddr cpuData with
        | true -> 
            match getMemLoc wordAddr cpuData with
            | Dat dl ->
                let byteValue = getCorrectByte dl addr
                setReg reg byteValue cpuData |> Ok
            | CodeSpace -> 
                (addr, " Trying to access memory where instructions are stored.")
                |> ``Run time error``
                |> Error
        | false -> 
            // (addr |> string, " You have not stored anything at this address, value is set to 0.")
            // ||> makeError 
            // |> ``Run time warning``
            // |> Error
            setReg reg 0u cpuData |> Ok

    /// Recursive function for storing multiple values at multiple memory addresses
    /// Need to check that the lists provided are the same length
    let rec setMultMem contentsLst addrLst cpuData : Result<DataPath, ExecuteError> =
        match addrLst, contentsLst with
        | mhead :: mtail, chead :: ctail when (List.length addrLst = List.length contentsLst) ->
            let newCpuData = updateMemData chead mhead cpuData
            Result.bind (setMultMem ctail mtail) newCpuData
        | [], [] -> cpuData |> Ok
        | _ -> failwith "Lists given to setMultMem function were of different sizes."
    
    /// Multiple setMemDatas 
    // let setMultMemData contentsLst = setMultMem (List.map DataLoc contentsLst)

        
    let fillRegs (vals : uint32 list) =
        List.zip [0..15] vals
        |> List.map (fun (r, v) -> (register r, v))
        |> Map.ofList

    let emptyRegs = 
        [0..15]
        |> List.map (fun _ -> 0u)
        |> fillRegs
        
    let initialDp () = {
            Fl = {N = false ; C = false ; Z = false ; V = false};
            Regs = emptyRegs;
            MM = Map.ofList []
        }
(*    let isMisc instr =
        match instr.PInstr with
        | Ok (CommonTop.IMISC _) ->
            true
        | _ ->
            false*)