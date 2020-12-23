open System

type serverMessage() = 
    [<DefaultValue>] val mutable command: String
    [<DefaultValue>] val mutable payload: Object

type clientMessage() = 
    [<DefaultValue>] val mutable controlFlag: Boolean
    [<DefaultValue>] val mutable command: String
    [<DefaultValue>] val mutable payload: Object

type ClientCoordinatorMessage()=
    [<DefaultValue>] val mutable Command: String
    [<DefaultValue>] val mutable NoOfClients: int