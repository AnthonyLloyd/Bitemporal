module SerializeTests

open System
open CsCheck
open Bitemporal

let zigzag = test "ZigZag" {

    test "examples_int" {
        DataSeries.ZigZag  0 |> Check.equal 0u
        DataSeries.ZigZag -1 |> Check.equal 1u
        DataSeries.ZigZag  1 |> Check.equal 2u
        DataSeries.ZigZag -2 |> Check.equal 3u
        DataSeries.ZigZag  2 |> Check.equal 4u
        DataSeries.ZigZag Int32.MaxValue |> Check.equal (UInt32.MaxValue-1u)
        DataSeries.ZigZag Int32.MinValue |> Check.equal UInt32.MaxValue
    }

    test "examples_long" {
        DataSeries.ZigZag  0L |> Check.equal 0UL
        DataSeries.ZigZag -1L |> Check.equal 1UL
        DataSeries.ZigZag  1L |> Check.equal 2UL
        DataSeries.ZigZag -2L |> Check.equal 3UL
        DataSeries.ZigZag  2L |> Check.equal 4UL
        DataSeries.ZigZag Int64.MaxValue |> Check.equal (UInt64.MaxValue-1UL)
        DataSeries.ZigZag Int64.MinValue |> Check.equal UInt64.MaxValue
    }

    test "examples_un_int" {
        DataSeries.UnZigZag 0u |> Check.equal  0
        DataSeries.UnZigZag 1u |> Check.equal -1
        DataSeries.UnZigZag 2u |> Check.equal  1
        DataSeries.UnZigZag 3u |> Check.equal -2
        DataSeries.UnZigZag 4u |> Check.equal  2
        DataSeries.UnZigZag (UInt32.MaxValue-1u) |> Check.equal Int32.MaxValue
        DataSeries.UnZigZag UInt32.MaxValue |> Check.equal Int32.MinValue
    }

    test "examples_un_long" {
        DataSeries.UnZigZag 0UL |> Check.equal  0L
        DataSeries.UnZigZag 1UL |> Check.equal -1L
        DataSeries.UnZigZag 2UL |> Check.equal  1L
        DataSeries.UnZigZag 3UL |> Check.equal -2L
        DataSeries.UnZigZag 4UL |> Check.equal  2L
        DataSeries.UnZigZag (UInt64.MaxValue-1UL) |> Check.equal Int64.MaxValue
        DataSeries.UnZigZag UInt64.MaxValue |> Check.equal Int64.MinValue
    }

    test "roundtrip_int" {
        let! expected = Gen.Int
        let actual = DataSeries.ZigZag expected |> DataSeries.UnZigZag
        Check.equal expected actual
    }

    test "roundtrip_long" {
        let! expected = Gen.Long
        let actual = DataSeries.ZigZag expected |> DataSeries.UnZigZag
        Check.equal expected actual
    }
}

let genUVint = Gen.ULong.[UVint.MinValue,UVint.MaxValue].Select(UVint)
let genVint = Gen.Long.[Vint.MinValue,Vint.MaxValue].Select(Vint)
let genTxId = Gen.UInt.[1u,1000u].Select(TxId)
let genDate = Gen.Date.[new DateTime(1950,1,1),new DateTime(2050,1,1)].Select(fun dt -> Date dt)

let primatives = test "primitives" {

    test "Vint_UVint" {
        let actual = DataSeries.ZigZag(Vint.MaxValue - Vint.MinValue)
        Check.equal (UVint.MaxValue - 1UL) actual
        let actual = DataSeries.ZigZag(Vint.MinValue - Vint.MaxValue)
        Check.equal (UVint.MaxValue - 2UL) actual
    }

    test "Vint" {
        let! expected = genUVint
        let mutable b = Bytes.Empty()
        b.Write(expected)
        b.Position <- 0
        let actual = b.Read()
        b.Return()
        Check.equal expected actual
    }

    test "Vint_Bytes" {
        let! expected = genUVint.Array
        let mutable b = Bytes.Empty()
        do
            for i = 0 to expected.Length-1 do
                b.Write(expected.[i])
        let a = b.Trim()
        b.Return()
        b <- Bytes(a)
        let actual = Array.zeroCreate expected.Length
        do
            for i = 0 to expected.Length-1 do
                actual.[i] <- b.Read()
        let position = b.Position
        Check.equal a.Length position
        Check.equal expected actual
    }

    test "DataSeries" {
        let! a = Gen.Dictionary(genTxId, Gen.Dictionary(genDate, genVint).[1,5]).[1,5]
        let expected =
            a |> Seq.collect (fun t -> t.Value |> Seq.map (fun v -> struct (v.Key, t.Key, v.Value)))
            |> Seq.toArray
        let mutable ds =
            let struct (fd, ft, fv) = expected.[0]
            DataSeries.Single(fd, ft, fv)
        for i = 1 to expected.Length-1 do
            let struct (d, t, v) = expected.[i]
            ds <- ds.Add(d, t, v)
        let actual = ds.ToArray()
        Check.info "%A" actual
        expected |> Array.sortInPlaceBy (fun struct (d,t,_) -> -int64 d.I,-int64 t.I)
        Check.equal expected actual
    }
}

let all =
    test "Serialize" {
        zigzag
        primatives
    }