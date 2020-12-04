open System
type clientMessage(controlFlag, command, payload) = 
    let mutable controlFlag: Boolean = controlFlag
    let mutable command: String = command
    let mutable payload: Object = payload

    member x.getControlFlag = controlFlag
    member x.getCommand = command
    member x.getPayload = payload

type ClientCoordinatorMessage(command,noOfClients) = 
    member this.Command:String = command
    member this.NoOfClients:int = noOfClients

type serverMessage(command, payload) = 
    let mutable command: String = command
    let mutable payload: Object = payload

    member x.getCommand = command
    member x.getPayload = payload