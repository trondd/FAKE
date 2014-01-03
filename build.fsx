#I @"tools/FAKE/tools/"
#r @"FakeLib.dll"

open Fake
open Fake.Git
open Fake.FSFHelper

// properties
let projectName = "FAKE"
let projectSummary = "FAKE - F# Make - Get rid of the noise in your build scripts."
let projectDescription = "FAKE - F# Make - is a build automation tool for .NET. Tasks and dependencies are specified in a DSL which is integrated in F#."
let authors = ["Steffen Forkmann"; "Mauricio Scheffer"; "Colin Bull"]
let mail = "forkmann@gmx.de"

let packages =
    ["FAKE.Core",projectDescription
     "FAKE.Gallio",projectDescription + " Extensions for Gallio"
     "FAKE.IIS",projectDescription + " Extensions for IIS"
     "FAKE.SQL",projectDescription + " Extensions for SQL Server"
     "FAKE.Experimental",projectDescription + " Experimental Extensions"
     "FAKE.Deploy.Lib",projectDescription + " Extensions for FAKE Deploy"
     projectName,projectDescription + " This package bundles all extensions."]

let buildVersion = if isLocalBuild then "0.0.1" else buildVersion

let buildDir = "./build"
let testDir = "./test"
let deployDir = "./Publish"
let docsDir = "./docs"
let apidocsDir = "./docs/apidocs/"
let nugetDir = "./nuget"
let reportDir = "./report"
let deployZip = deployDir @@ sprintf "%s-%s.zip" projectName buildVersion
let packagesDir = "./packages"

let additionalFiles = [
    "License.txt"
    "README.markdown"
    "help/changelog.md"]

// Targets
Target "Clean" (fun _ -> CleanDirs [buildDir; testDir; deployDir; docsDir; apidocsDir; nugetDir; reportDir])

Target "RestorePackages" RestorePackages

Target "CopyFSharpFiles" (fun _ ->
    ["./tools/FSharp/FSharp.Core.optdata"
     "./tools/FSharp/FSharp.Core.sigdata"]
      |> CopyTo buildDir
)

open Fake.AssemblyInfoFile

Target "SetAssemblyInfo" (fun _ ->
    let common = [
         Attribute.Product "FAKE - F# Make"
         Attribute.Version buildVersion
         Attribute.InformationalVersion buildVersion
         Attribute.FileVersion buildVersion]

    [Attribute.Title "FAKE - F# Make Command line tool"
     Attribute.Guid "fb2b540f-d97a-4660-972f-5eeff8120fba"] @ common
    |> CreateFSharpAssemblyInfo "./src/app/FAKE/AssemblyInfo.fs"

    [Attribute.Title "FAKE - F# Make Deploy tool"
     Attribute.Guid "413E2050-BECC-4FA6-87AA-5A74ACE9B8E1"] @ common
    |> CreateFSharpAssemblyInfo "./src/app/Fake.Deploy/AssemblyInfo.fs"

    [Attribute.Title "FAKE - F# Make Deploy Web App"
     Attribute.Guid "2B684E7B-572B-41C1-86C9-F6A11355570E"] @ common
    |> CreateFSharpAssemblyInfo "./src/deploy.web/Fake.Deploy.Web.App/AssemblyInfo.fs"

    [Attribute.Title "FAKE - F# Make Deploy Web"
     Attribute.Guid "27BA7705-3F57-47BE-B607-8A46B27AE876"] @ common
    |> CreateCSharpAssemblyInfo "./src/deploy.web/Fake.Deploy.Web/AssemblyInfo.cs"

    [Attribute.Title "FAKE - F# Make Deploy Lib"
     Attribute.Guid "AA284C42-1396-42CB-BCAC-D27F18D14AC7"] @ common
    |> CreateFSharpAssemblyInfo "./src/app/Fake.Deploy.Lib/AssemblyInfo.fs"

    [Attribute.Title "FAKE - F# Make Lib"
     Attribute.InternalsVisibleTo "Test.FAKECore"
     Attribute.Guid "d6dd5aec-636d-4354-88d6-d66e094dadb5"] @ common
    |> CreateFSharpAssemblyInfo "./src/app/FakeLib/AssemblyInfo.fs"

    [Attribute.Title "FAKE - F# Make SQL Lib"
     Attribute.Guid "A161EAAF-EFDA-4EF2-BD5A-4AD97439F1BE"] @ common
    |> CreateFSharpAssemblyInfo "./src/app/Fake.SQL/AssemblyInfo.fs"

    [Attribute.Title "FAKE - F# Make Experimental Lib"
     Attribute.Guid "5AA28AED-B9D8-4158-A594-32FE5ABC5713"] @ common
    |> CreateFSharpAssemblyInfo "./src/app/Fake.Experimental/AssemblyInfo.fs"
)

Target "BuildSolution" (fun _ ->
    MSBuildWithDefaults "Build" ["./FAKE.sln"]
    |> Log "AppBuild-Output: "
)

/// Specifies the fsformatting executable
let mutable fsformattingPath = findToolInSubPath "fsformatting.exe" (currentDirectory @@ "tools" @@ "fsformatting")
    
/// Specifies a global timeout for fsformatting.exe
let mutable fsformattingTimeOut = System.TimeSpan.MaxValue

/// Runs fsformatting.exe with the given command in the given repository directory.
let runFSFormattingCommand workingDir command =
    if 0 <> ExecProcess (fun info ->  
        info.FileName <- fsformattingPath
        info.WorkingDirectory <- workingDir
        info.Arguments <- command) fsformattingTimeOut
    then
        failwithf "FSharp.Formatting %s failed." command

let CreateDocs workingDir source output template projectParameters =    
    let command =
        projectParameters 
        |> Seq.map (fun (k,v) -> [k;v]) 
        |> Seq.concat
        |> Seq.append (
            [ "literate";
              "--processdirectory";
              "--inputdirectory"; source
              "--templatefile"; template
              "--outputDirectory"; output;
              "--replacements" ])
        |> Seq.map (fun s -> if s.StartsWith "\"" then s else sprintf "\"%s\"" s)
        |> separated " " 

    runFSFormattingCommand workingDir command
    printfn "Successfully generated docs for %s" source
           
let CreateDocsForDlls workingDir projectParameters dllFiles =
    let templatesDir = "./help/templates/reference/"    
    let command =
        projectParameters 
        |> Seq.map (fun (k,v) -> [k;v])
        |> Seq.concat
        |> Seq.append (
            [ "metadataformat"
              "--generate";
              "--outdir"; apidocsDir;
              "--layoutroots"; 
              "./help/templates/"; templatesDir;
              "--parameters" ])
        |> Seq.map (fun s -> if s.StartsWith "\"" then s else sprintf "\"%s\"" s)
        |> separated " " 

    for file in dllFiles do 
        let command = command + sprintf " --dllfiles \"%s\"" file
                
        runFSFormattingCommand workingDir command
        printfn "Successfully generated docs for DLL %s" file

Target "GenerateDocs" (fun _ ->
    let source = "./help"
    let template = "./help/templates/template-project.html"
    let projInfo =
      [ "page-description", "FAKE - F# Make"
        "page-author", separated ", " authors
        "project-author", separated ", " authors
        "github-link", "http://github.com/fsharp/fake"
        "project-github", "http://github.com/fsharp/fake"
        "project-nuget", "https://www.nuget.org/packages/FAKE"
        "root", "http://fsharp.github.io/FAKE"
        "project-name", "FAKE - F# Make" ]

    CreateDocs "." source docsDir template projInfo
   
    let dllFiles = 
        !! "./build/FakeLib.dll"
          ++ "./build/**/Fake.*.dll"
          -- "./build/**/Fake.Experimental.dll"
          |> Seq.toList
        
    CreateDocsForDlls "." projInfo dllFiles

    WriteStringToFile false "./docs/.nojekyll" ""

    CopyDir (docsDir @@ "content") "help/content" allFiles
    CopyDir (docsDir @@ "pics") "help/pics" allFiles
)

Target "CopyLicense" (fun _ ->
    CopyTo buildDir additionalFiles
)

Target "BuildZip" (fun _ ->
    !! (buildDir @@ @"**/*.*")
      -- "*.zip"
      -- "**/*.pdb"
      |> Zip buildDir deployZip
)

Target "Test" (fun _ ->
    !! (testDir @@ "Test.*.dll")
    |> MSpec (fun p ->
            {p with
                ExcludeTags = ["HTTP"]
                HtmlOutputDir = reportDir})
)

Target "ZipDocumentation" (fun _ ->
    !! (docsDir @@ @"**/*.*")
       |> Zip docsDir (deployDir @@ sprintf "Documentation-%s.zip" buildVersion)
)

Target "CreateNuGet" (fun _ ->
    for package,description in packages do
        let nugetDocsDir = nugetDir @@ "docs"
        let nugetToolsDir = nugetDir @@ "tools"

        CleanDir nugetDocsDir
        CleanDir nugetToolsDir

        DeleteFile "./build/FAKE.Gallio/Gallio.dll"

        match package with
        | p when p = projectName ->
            !! (buildDir @@ "**/*.*") |> Copy nugetToolsDir
            CopyDir nugetToolsDir @"./lib/fsi" allFiles
            CopyDir nugetDocsDir docsDir allFiles
        | p when p = "FAKE.Core" ->
            !! (buildDir @@ "*.*") |> Copy nugetToolsDir
            CopyDir nugetToolsDir @"./lib/fsi" allFiles
            CopyDir nugetDocsDir docsDir allFiles
        | _ ->
            CopyDir nugetToolsDir (buildDir @@ package) allFiles
            CopyTo nugetToolsDir additionalFiles
        !! (nugetToolsDir @@ "*.pdb") |> DeleteFiles

        (SemVerHelper.parse buildVersion).Patch.ToString()
        |> WriteStringToFile false (nugetToolsDir @@ "PatchVersion.txt")

        NuGet (fun p ->
            {p with
                Authors = authors
                Project = package
                Description = description
                OutputPath = nugetDir
                Summary = projectSummary
                Dependencies =
                    if package <> "FAKE.Core" && package <> projectName then
                      ["FAKE.Core", RequireExactly (NormalizeVersion buildVersion)]
                    else p.Dependencies
                AccessKey = getBuildParamOrDefault "nugetkey" ""
                Publish = hasBuildParam "nugetkey"
                ToolPath = "./tools/NuGet/nuget.exe"  }) "fake.nuspec"
)

Target "ReleaseDocs" (fun _ ->
    CleanDir "gh-pages"
    CommandHelper.runSimpleGitCommand "" "clone -b gh-pages --single-branch git@github.com:fsharp/FAKE.git gh-pages" |> printfn "%s"

    fullclean "gh-pages"
    CopyRecursive "docs" "gh-pages" true |> printfn "%A"
    CopyFile "gh-pages" "./Samples/FAKE-Calculator.zip"
    CommandHelper.runSimpleGitCommand "gh-pages" "add . --all" |> printfn "%s"
    CommandHelper.runSimpleGitCommand "gh-pages" (sprintf "commit -m \"Update generated documentation %s\"" buildVersion) |> printfn "%s"
    Branches.push "gh-pages"
)

Target "Default" DoNothing

// Dependencies
"Clean"
    ==> "RestorePackages"
    ==> "CopyFSharpFiles"
    =?> ("SetAssemblyInfo",not isLocalBuild )
    ==> "BuildSolution"
    =?> ("Test",not isLinux )
    ==> "CopyLicense"
    ==> "BuildZip"
    =?> ("GenerateDocs",    isLocalBuild && not isLinux )
    =?> ("ZipDocumentation",not isLinux )
    =?> ("CreateNuGet",     not isLinux )
    ==> "Default"
    ==> "ReleaseDocs"

// start build
RunTargetOrDefault "Default"
