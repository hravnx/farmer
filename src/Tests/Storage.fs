module Storage

open Expecto
open Farmer
open Farmer.Builders
open Farmer.Storage
open Microsoft.Azure.Management.Storage
open Microsoft.Azure.Management.Storage.Models
open Microsoft.Rest
open System

/// Client instance needed to get the serializer settings.
let client = new StorageManagementClient(Uri "http://management.azure.com", TokenCredentials "NotNullOrWhiteSpace")
let tests = testList "Storage Tests" [
    test "Can create a basic storage account" {
        let resource =
            let account = storageAccount {
                name "mystorage123"
                sku Premium_LRS
                enable_data_lake
            }
            arm { add_resource account }
            |> findAzureResources<StorageAccount> client.SerializationSettings
            |> List.head

        resource.Validate()
        Expect.equal resource.Name "mystorage123" "Account name is wrong"
        Expect.equal resource.Sku.Name "Premium_LRS" "SKU is wrong"
        Expect.isTrue resource.IsHnsEnabled.Value "Hierarchical namespace not enabled"
    }
    test "Creates containers correctly" {
        let resources : BlobContainer list =
            let account = storageAccount {
                name "storage"
                add_blob_container "blob"
                add_private_container "private"
                add_public_container "public"
            }
            [ for i in 1 .. 3 do account |> getResourceAtIndex client.SerializationSettings i ]

        Expect.equal resources.[0].Name "storage/default/blob" "blob name is wrong"
        Expect.equal resources.[0].PublicAccess.Value PublicAccess.Blob "blob access is wrong"
        Expect.equal resources.[1].Name "storage/default/private" "private name is wrong"
        Expect.equal resources.[1].PublicAccess.Value PublicAccess.None "private access is wrong"
        Expect.equal resources.[2].Name "storage/default/public" "public name is wrong"
        Expect.equal resources.[2].PublicAccess.Value PublicAccess.Container "container access is wrong"
    }
    test "Creates file shares correctly" {
        let resources : FileShare list =
            let account = storageAccount {
                name "storage"
                add_file_share "share1"
                add_file_share_with_quota "share2" 1024<Gb>
            }
            [ for i in 1 .. 2 do account |> getResourceAtIndex client.SerializationSettings i ]

        Expect.equal resources.[0].Name "storage/default/share1" "file share name for 'share1' is wrong"
        Expect.equal resources.[1].Name "storage/default/share2" "file share name for 'share2' is wrong"
        Expect.equal resources.[1].ShareQuota (Nullable 1024) "file share quota for 'share2' is wrong"
    }
    test "Rejects invalid storage accounts" {
        let check (v:string) m = Expect.equal (StorageAccountName.Create v) (Error ("Storage account names " + m))

        check "" "cannot be empty" "Name too short"
        check "zz" "min length is 3, but here is 2 ('zz')" "Name too short"
        check "abcdefghij1234567890abcde" "max length is 24, but here is 25 ('abcdefghij1234567890abcde')" "Name too long"
        check "zzzT" "can only contain lowercase letters ('zzzT')" "Upper case character allowed"
        check "zzz!" "can only contain alphanumeric characters ('zzz!')" "Non alpha numeric character allowed"
        Expect.equal (StorageResourceName.Create "abcdefghij1234567890abcd" |> Result.get |> fun name -> name.ResourceName) (ResourceName "abcdefghij1234567890abcd") "Should have created a valid storage account name"
    }
    test "Rejects invalid storage resource names" {
        let check (v:string) m = Expect.equal (StorageResourceName.Create v) (Error ("Storage resource names " + m))

        check "" "cannot be empty" "Name too short"
        check "zz" "min length is 3, but here is 2 ('zz')" "Name too short"
        let longName = Array.init 64 (fun _ -> 'a') |> String
        check longName ("max length is 63, but here is 64 ('" + longName + "')") "Name too long"
        check "zzzT" "can only contain lowercase letters ('zzzT')" "Upper case character allowed"
        check "zz!z" "can only contain letters, numbers, and the dash (-) character ('zz!z')" "Bad character allowed"
        check "zzz--z" "do not allow consecutive dashes ('zzz--z')" "Double dash allowed"
        check "-zz" "must start with an alphanumeric character ('-zz')" "Start with dash"
        check "zz-" "must end with an alphanumeric character ('zz-')" "End with dash"

        Expect.equal (StorageResourceName.Create "abcdefghij1234567890abcd" |> Result.get |> fun name -> name.ResourceName) (ResourceName "abcdefghij1234567890abcd") "Should have created a valid storage resource name"
    }
]