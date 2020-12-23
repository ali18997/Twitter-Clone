open System
type tweet() = 
    inherit Object()

    [<DefaultValue>] val mutable sender: String
    [<DefaultValue>] val mutable tweet: String
    [<DefaultValue>] val mutable mentions: List<String>
    [<DefaultValue>] val mutable hashtags: List<String>

type subscribedTo() =
    [<DefaultValue>] val mutable client: String
    [<DefaultValue>] val mutable subscribedClients: List<String>

type query() =
    inherit Object()

    [<DefaultValue>] val mutable typeOf: String
    [<DefaultValue>] val mutable matching: String