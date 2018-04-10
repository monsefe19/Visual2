module Errors

    open CommonData


        /// Failure message for impossible match cases
    let alwaysMatchesFM = "Should never happen! Match statement always matches."

    let noErrorsFM = "Should never happen! No errors at this stage."

    // *************************************************************************
    // Error messages for instruction parsing
    // *************************************************************************

    /// Error message for `Invalid register`
    let notValidRegEM = " is not a valid register."

    /// Error message for `Invalid offset`
    let notValidOffsetEM = " is not a valid offset."

    /// Error message for `Invalid instruction`
    let notValidFormatEM = " is not a valid instruction format."

    /// Error message for `Invalid literal`
    let notValidLiteralEM = " is not a valid literal."

    /// Error message for `Invalid shift` or `Invalid second operand`
    let notValidRegLitEM = " is not a valid literal or register."

    /// Error message for `Invalid flexible second operand`
    let notValidFlexOp2EM = " is an invalid flexible second operand"

    /// Error message for `Invalid suffix`
    let notValidSuffixEM = " is not a valid suffix for this instruction."

    let notImplementedInsEM = " is not a recognised instruction."
    // *************************************************************************
    // Error messages for instruction execution
    // *************************************************************************



    type ErrCode =
        | ``Invalid syntax``
        | ``Undefined parse``
        | ``Invalid literal`` 
        | ``Invalid second operand``
        | ``Invalid flexible second operand``
        | ``Invalid memory address``
        | ``Invalid offset``
        | ``Invalid register``
        | ``Invalid shift``
        | ``Invalid suffix``
        | ``Invalid instruction``
        | ``Invalid expression``
        | ``Invalid expression list``
        | ``Invalid fill``
        | ``Label required``
        | ``Undefined symbol``
        | ``Unimplemented instruction``

    type ParseError = ErrCode * string * string
    
    type ExecuteError =
        | NotInstrMem of uint32 // Address where there is no instruction
        | ``Run time error`` of uint32 * string
        | ``Unknown symbol runtime error`` of string list
        | EXIT

    let makePE code txt message = (code,txt,message) |> Error
   

    /// A function to combine results or forward errors.
    let combineError (res1:Result<'T1,'E>) (res2:Result<'T2,'E>) : Result<'T1 * 'T2, 'E> =
        match res1, res2 with
        | Error e1, _ -> Error e1
        | _, Error e2 -> Error e2
        | Ok rt1, Ok rt2 -> Ok (rt1, rt2)

    /// A function that combines two results by applying a function on them as a pair, or forwards errors.
    let combineErrorMapResult (res1:Result<'T1,'E>) (res2:Result<'T2,'E>) (mapf:'T1 -> 'T2 -> 'T3) : Result<'T3,'E> =
        combineError res1 res2
        |> Result.map (fun (r1,r2) -> mapf r1 r2)
    
    /// A function that applies a possibly erroneous function to a possibly erroneous argument, or forwards errors.
    let applyResultMapError (res:Result<'T1->'T2,'E>) (arg:Result<'T1,'E>) =
        match arg, res with
        | Ok arg', Ok res' -> res' arg' |> Ok
        | _, Error e -> e |> Error
        | Error e, _ -> e |> Error

    let mapErrorApplyResult (arg:Result<'T1,'E>) (res:Result<'T1->'T2,'E>) =
        match arg, res with
        | Ok arg', Ok res' -> res' arg' |> Ok
        | _, Error e -> e |> Error
        | Error e, _ -> e |> Error

    let condenseResultList transform (lst: Result<'a,'b> list) : Result<'a list,'b> =
        let rec condenser' inlst outlst = 
            match inlst with
            | head :: tail ->
                match head with
                | Ok res -> condenser' tail ((transform res) :: outlst)
                | Error e -> e |> Error
            | [] -> List.rev outlst |> Ok
        condenser' lst [] 