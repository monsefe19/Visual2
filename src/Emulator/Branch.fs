//////////////////////////////////////////////////////////////////////////////////////////
//                   Branch Instruction Implementation Module
//////////////////////////////////////////////////////////////////////////////////////////

module Branch
    open CommonData
    open CommonLex
    open Expressions
    open Errors
    open Helpers
    
    type Instr =
        | B of uint32
        | BL of uint32
        | END

    let branchTarget = function B t | BL t -> [t] | END -> []

    // Branch instructions have no suffixes
    let branchSpec = {
        InstrC = BRANCH
        Roots = ["B";"BL";"END"]
        Suffixes = [""]
    }

    /// map of all possible opcodes recognised
    let opCodes = opCodeExpand branchSpec

    let parse (ls: LineData) : Parse<Instr> option =
        let parse' (_iClass, (root,suffix,cond)) =
            let (WA la) = ls.LoadAddr // address this instruction is loaded into memory
            let target = resolveOp ls.SymTab ls.Operands
            match root, target with
            | "B", Ok t -> B t |> Ok
            | "BL", Ok t -> BL t |> Ok
            | "B",Error t 
            | "BL", Error t -> Error t
            | "END",_ when ls.Operands="" -> Ok END
            | "END",_ -> makePE ``Invalid instruction`` ls.Operands "END does not take an operand"
            | _ -> failwithf "What? Unexpected root in Branch.parse'%s'" root
            |> fun ins -> copyParse ls ins cond
        let OPC = ls.OpCode.ToUpper ()
        if  Map.containsKey OPC opCodes // lookup opcode to see if it is known
        then parse' opCodes.[OPC] |> Some
        else None

    /// Parse Active Pattern used by top-level code
    let (|IMatch|_|) = parse
  

    /// Execute a Branch instruction
    let executeBranch ins cpuData =
        let nxt = getPC cpuData + 4u // Address of the next instruction
        match ins with
        | B addr -> 
            cpuData 
            |> updateReg (addr - 4u) R15
            |> Ok
        | BL addr ->
            cpuData
            |> updateReg (addr - 4u) R15
            |> updateReg nxt R14
            |> Ok
        | END ->
            EXIT |> Error
       

    