module iSharesTests

open System
open System.IO
open System.Text
open System.Globalization
open System.Collections.Generic
open Bitemporal
open Demo

module Csv =
    let load (reader:TextReader) (delimiter:char) =
        seq {
            let QUOTE, SPACE, LF, CR, EOF = 34, 32, 10, 13, -1
            let data = List()
            let chars = StringBuilder()
            let mutable b = 0
            let inline read() = b <- reader.Read(); b
            while read() <> EOF || data.Count <> 0 do
                if b = EOF || b = LF then
                    if data.Count <> 0 || chars.Length <> 0 then
                        let rec lastSpace b =
                            if b = -1 || int chars.[b] <> SPACE then b
                            else lastSpace (b-1)
                        b <- lastSpace (chars.Length - 1)
                        data.Add(chars.ToString(0, b + 1))
                        chars.Clear() |> ignore
                        yield data.ToArray()
                        data.Clear()
                elif b = int delimiter then
                    let rec lastSpace b =
                        if b = -1 || int chars.[b] <> SPACE then b
                        else lastSpace (b-1)
                    b <- lastSpace (chars.Length - 1)
                    data.Add(chars.ToString(0, b + 1))
                    chars.Clear() |> ignore
                elif b = QUOTE then
                    while read() <> EOF && (b <> QUOTE || reader.Peek() = QUOTE) do
                        if b = QUOTE then reader.Read() |> ignore
                        if b <> SPACE || chars.Length <> 0 then chars.Append((char)b) |> ignore
                elif b <> CR && (b <> SPACE || chars.Length <> 0) then chars.Append((char)b) |> ignore
        }

[<Struct>]
type Text =
    internal
    | Text of string
    static member (+)(Text t1,Text t2) = Text(t1+t2)
    static member (+)(s:string,Text t) = Text(s+t)
    static member (+)(Text t,s:string) = Text(t+s)
    override t.ToString() = let (Text s) = t in s

module Text =
    let ofString s =
        if String.IsNullOrWhiteSpace s then None
        else s.Trim() |> Text |> Some
    let toString (Text s) = s
    let length (Text s) = s.Length
    let inline tryParse (t:Text) =
        let mutable r = Unchecked.defaultof<_>
        if (^a : (static member TryParse: string * ^a byref -> bool) (toString t, &r))
        then Some r else None

module Result =
    let apply f x =
        match f,x with
        | Ok f, Ok v -> Ok (f v)
        | Error f, Ok _ -> Error f
        | Ok _, Error f -> Error [f]
        | Error f1, Error f2 -> Error (f2::f1)
    let ofOption format =
        let sb = Text.StringBuilder()
        Printf.kbprintf (fun () ->
            function
            | Some x -> Ok x
            | None -> sb.ToString() |> Error
        ) sb format
    let ofVOption format =
        let sb = Text.StringBuilder()
        Printf.kbprintf (fun () ->
            function
            | ValueSome x -> Ok x
            | ValueNone -> sb.ToString() |> Error
        ) sb format
    let toOption (r:Result<'r,'e>) =
        match r with
        | Ok r -> Some r
        | Error e -> None

type ResultBuilder() =
    member inline __.Bind(v,f) = Result.bind f v
    member inline __.Return v = Ok v
    member inline __.ReturnFrom o = o

[<AutoOpen>]
module ResultAutoOpen =
    let (<*>) = Result.apply
    let attempt = ResultBuilder()

let rec decimalPlaces (d:decimal) i =
    if Decimal.Truncate d = d then i
    else decimalPlaces (d * 10M) (i+1)

type PositionRow = {
    // Fund: Text // position
    // Instrument: instrument // position
    Nominal: decimal // position, 0dp
    Weight: decimal // ignore
    MarketValue: decimal // ignore
    NotionalValue: decimal option // ignore
    
    ISIN: Text option // instrument //
    IssuerTicker: Text // instrument //
    MarketCurrency: Text // instrument
    Name: Text // instrument
    AssetClass: Text option // instrument
    Sector: Text option // instrument //
    Exchange: Text option // instrument
    Country: Text option // instrument //
    Coupon: decimal option // instrument, 2dp
    Maturity: Date option // instrument
    Price: decimal // instrument, 2dp
    Duration: decimal option // instrument, 2dp
}

let column (headerRow:Text option list) =
    let colMap = List.length headerRow |> MapSlim
    List.iteri (fun i o ->
        match o with
        | Some t ->
            if colMap.IndexOf(t) = -1 then colMap.[t] <- i
        | None -> ()
    ) headerRow
    fun h ->
        let i = Text.ofString h |> Option.get |> colMap.IndexOf
        if i = -1 then String.Concat("No ", h, " column") |> Error
        else
            let i = colMap.Value i
            Ok (fun row ->
                List.tryItem i row
                |> Result.ofOption "No %s column for row" h
                |> Result.bind (Result.ofOption "Empty %s cell" h)
            )

let onlyOneCellError = "Only one cell"
let xmlFileError = "XML file not cs"

module Date =
    let private dateFormats = [|"dd/MMM/yyyy"|]
    let tryParse (t:Text) =
        match DateTime.TryParseExact(Text.toString t, dateFormats, CultureInfo.InvariantCulture,
                                            DateTimeStyles.AssumeUniversal
                                        ||| DateTimeStyles.AdjustToUniversal
                                        ||| DateTimeStyles.AllowWhiteSpaces) with
        |true, d -> Date d |> Some
        |false, _ -> None

let parsePositionFile (data:Text option list list) : Result<Date * Result<PositionRow,string list> list,string> =
    attempt {
        do! if List.length data = 1 && List.head data |> List.length = 1 then Error onlyOneCellError else Ok ()
        let! firstRow = List.tryItem 0 data |> Result.ofOption "No first row"
        let! firstCell = List.tryItem 0 firstRow |> Result.ofOption "No first row first cell"
        do! match firstCell with | Some (Text s) when s.Contains("""<?xml""") -> Error xmlFileError |_-> Ok ()
        let! dateCell = List.tryItem 1 firstRow |> Result.ofOption "No first row second cell"
        let! date = Option.bind Date.tryParse dateCell |> Result.ofOption "Incorrect date format %A" dateCell
        let! headerRow = List.tryItem 2 data |> Result.ofOption "No header row"
        let column = column headerRow
        let! issuerTicker = column "Issuer Ticker"
        let! name = column "Name"
        let! assetClass = column "Asset Class"
        let! weight = column "Weight (%)"
        let! price = column "Price"
        let! nominal = column "Nominal"
        let! marketValue = column "Market Value"
        let! sector = column "Sector"
        let! isin = column "ISIN"
        let! exchange = column "Exchange"
        let! country = column "Country"
        let! currency = match column "Market Currency" with | Ok m -> Ok m | Error _ -> column "Currency"
        let notionalValue = column "Notional Value"
        let coupon = column "Coupon (%)"
        let maturity = column "Maturity"
        let duration = column "Duration"
        let position r =
            Ok (fun t n a w p o m v s i r u e y c d ->
                {IssuerTicker=t;Name=n;AssetClass=a;Weight=w;Price=p;Nominal=o;MarketValue=m;NotionalValue=v;Sector=s
                 ISIN=i;Coupon=r;Maturity=u;Exchange=e;Country=y;MarketCurrency=c;Duration=d})
            <*> issuerTicker r
            <*> name r
            <*> (assetClass r |> Result.map (fun i -> if string i="-" then None else Some i))
            <*> (weight r |> Result.bind (fun t -> Text.tryParse t |> Result.ofOption "Weight not a decimal: %s" (string t)))
            <*> (price r |> Result.bind (Text.tryParse >> Result.ofOption "Price not a decimal"))
            <*> (nominal r |> Result.bind (Text.tryParse >> Result.ofOption "Nominal not valid"))
            <*> (marketValue r |> Result.bind (Text.tryParse >> Result.ofOption "Market Value not valid"))
            <*> (notionalValue |> Result.bind (fun f -> f r) |> Result.bind (Text.tryParse >> Ok))
            <*> (sector r |> Result.map (fun i -> if string i="-" then None else Some i))
            <*> (isin r |> Result.map (fun i -> if string i="-" then None else Some i))
            <*> (coupon |> Result.bind (fun f -> f r) |> Result.bind (Text.tryParse >> Result.ofOption "Coupon not a decimal") |> Result.toOption |> Ok)
            <*> (maturity |> Result.bind (fun f -> f r) |> Result.bind (Date.tryParse >> Result.ofOption "Maturity not a date") |> Result.toOption |> Ok)
            <*> (exchange r |> Result.map (fun i -> if string i="-"||string i="NO MARKET (E.G. UNLISTED)" then None else Some i))
            <*> (country r |> Result.map Some)
            <*> (currency r)
            <*> (duration |> Result.bind (fun f -> f r) |> Result.bind (Text.tryParse >> Result.ofOption "Duration not a decimal") |> Result.toOption |> Ok)
        let positions =
            Seq.skip 3 data
            |> Seq.where (fun r -> List.length r > 1)
            |> Seq.map position
            |> Seq.toList
        return date, positions
    }

[<Struct;CustomEquality;NoComparison>]
type AssetKey =
    | InstrumentKey of ticker:Text * currency:Text * isin:Text option * name:Text * sector:Text option
    interface IEquatable<AssetKey> with
        member m.Equals (InstrumentKey(xt,xc,xi,xn,xs)) =
            let (InstrumentKey(yt,yc,yi,yn,ys)) = m
            xt = yt && xc = yc && xi = yi && xn = yn && xs = ys
    override m.GetHashCode() =
        match  m with InstrumentKey(xt,xc,xi,xn,xs) -> xt.GetHashCode() ^^^ xc.GetHashCode() ^^^ hash xi ^^^ xn.GetHashCode() ^^^ hash xs
    override m.Equals(o:obj) =
        let (InstrumentKey(yt,yc,yi,yn,ys)) = o :?> AssetKey
        let (InstrumentKey(xt,xc,xi,xn,xs)) = m
        xt = yt && xc = yc && xi = yi && xn = yn && xs = ys
    static member ofPosition (p:PositionRow) =
        InstrumentKey (p.IssuerTicker, p.MarketCurrency, p.ISIN, p.Name, p.Sector)

let all = test "ishares" {
    let iSharesDir = __SOURCE_DIRECTORY__ + "/../data/iShares"

    test "load" {

        let one = Fixed2.Round 1.0
        let archive = FundArchive()
        let snapshot = archive.SnapshotLatest
        let sd = snapshot.[Date(DateTime(1974,8,2))]
        let user = sd.UserCollection.New("ant")
        let dummyAsset = FundArchive.Asset(sd, FundArchive.AssetId(0u))
        let usd = sd.AssetCollection.New("USD", dummyAsset, one, "", "", "", "")
        let allFundsId = sd.AssetCollection.New("ALL", usd, one, "", "", "", "").Id
        let time = Time(DateTime.Now)
        snapshot.Commit(user, time, "initial setup")

        let assetsKeyToAssetId = MapSlim<AssetKey, FundArchive.AssetId>()
        let currencyAsset = MapSlim<Text, FundArchive.Asset>()

        let mutable assetCount = 0u
        let mutable fundCount = 0

        let load (fn:string) (date:Date) (fund:string) (rows:PositionRow list) =
            let snapshot = archive.SnapshotLatest
            let sd = snapshot.[date]
            let allFunds = sd.AssetCollection.[allFundsId]
            let fund =
                match allFunds.Positions |> Seq.tryFind (fun f -> f.Asset.Name = fund) with
                | Some f -> f.Asset
                | None ->
                    printfn "%s" fund
                    let fund = sd.AssetCollection.New(fund, usd, one, "", "", "", "")
                    allFunds.Positions.Add(sd.PositionCollection.New(fund, one))
                    fund
            fundCount <- Seq.length allFunds.Positions
            for row in rows do
                let asset =
                    let assetKey = AssetKey.ofPosition row
                    let price = Fixed2.Round row.Price
                    match assetsKeyToAssetId.IndexOf assetKey with
                    | -1 ->
                        let ccyAsset =
                            match currencyAsset.IndexOf row.MarketCurrency with
                            | -1 ->
                                let newCurrency = sd.AssetCollection.New(string row.MarketCurrency, dummyAsset, one, "", "", "", "")
                                currencyAsset.[row.MarketCurrency] <- newCurrency
                                newCurrency
                            | i -> currencyAsset.Value i
                        let asset = sd.AssetCollection.New(string row.Name, ccyAsset, price,
                                        (match row.ISIN with Some t -> string t | None -> ""),
                                        string row.IssuerTicker,
                                        (match row.Sector with Some t -> string t | None -> ""),
                                        (match row.Country with Some t -> string t | None -> ""))
                        assetsKeyToAssetId.[assetKey] <- asset.Id
                        asset
                    | i ->
                        let asset = sd.AssetCollection.[assetsKeyToAssetId.Value i]
                        if asset.Price <> price then
                            let mutable asset = asset
                            asset.Price <- price
                        asset
                match fund.Positions |> Seq.tryFind (fun p -> p.Asset = asset) with
                | Some p ->
                    let q = Fixed2.Round row.Nominal
                    if p.Quantity <> q then
                        let mutable p = p
                        p.Quantity <- q
                | None ->
                    let p = sd.PositionCollection.New(asset, Fixed2.Round row.Nominal)
                    fund.Positions.Add(p)
            snapshot.Commit(user, Time(DateTime.Now), fn)
            let counts = archive.SnapshotLatest.Counts
            if counts.[0] % 100u = 0u then
                let message = if assetCount + 1u = counts.[2] then
                                let asset = archive.SnapshotLatest.[Date(DateTime.Now)].AssetCollection.[FundArchive.AssetId assetCount]
                                asset.Name.ToString() + " " + asset.Price.ToString()
                              else ""
                printfn "%7i %7i %7i %7i %s FC=%i" counts.[0] counts.[1] counts.[2] counts.[3] message fundCount
            assetCount <- counts.[2]

        Directory.GetFiles(iSharesDir, "*", SearchOption.AllDirectories)
        |> Seq.map (fun filename ->
            use fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)
            use sr = new StreamReader(fs)
            filename,
            Csv.load sr ','
            |> Seq.map (Seq.map Text.ofString >> Seq.toList)
            |> Seq.toList
            |> parsePositionFile
        )
        |> Seq.map (fun (fn,r) ->
            match r with
            | Error e -> Some (fn,e)
            | Ok (_,r) when r |> List.exists (function | Error _ -> true | Ok _ -> false) ->
                let errorRows = r |> List.where (function | Error _ -> true | Ok _ -> false) |> List.truncate 10
                Some (fn, sprintf "%A" errorRows)
            | Ok (date,r) ->
                let rows = r |> List.map (function | Error _ -> failwith "" | Ok i -> i)
                let fund = Path.GetFileNameWithoutExtension(fn)
                let fund = if fund.Contains('_') then fund.Substring(0, fund.IndexOf('_')) else fund
                load fn date fund rows
                None
        )
        |> Seq.choose id
        |> Seq.tryHead
        |> Check.equal None

        archive.Save("data/fund.arc")
    }
}