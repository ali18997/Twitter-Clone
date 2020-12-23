open System
type tweet(sender, tweet, mentions, hashtags) = 
    inherit Object()

    let mutable sender: String = sender
    let mutable tweet: String = tweet
    let mentions: List<String> = mentions
    let hashtags: List<String> = hashtags

    member x.getSender = sender
    member x.getTweet = tweet
    member x.getMentions = mentions
    member x.getHashtags = hashtags

type query(typeOf, matching) =
    inherit Object()

    let mutable typeOf: String = typeOf
    let mutable matching: String = matching

    member x.getTypeOf = typeOf
    member x.getMatching = matching

type subscribedTo(client, subscribedClients) =
    let mutable client: String = client
    let mutable subscribedClients: List<String> = subscribedClients

    member x.getClient = client
    member x.getSubscribedClients = subscribedClients
    member x.addSubscribedClients(newClient) = subscribedClients <- subscribedClients @ [newClient]