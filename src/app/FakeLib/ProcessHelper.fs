﻿[<AutoOpen>]
module Fake.ProcessHelper

open System
open System.Diagnostics
open System.IO
open System.Threading

let mutable redirectOutputToTrace = false

/// Runs the given process
/// returns the exit code
let execProcessAndReturnExitCode infoAction =
  use p = new Process()
  p.StartInfo.UseShellExecute <- false
  infoAction p.StartInfo
  p.StartInfo.RedirectStandardOutput <- true
  p.StartInfo.RedirectStandardError <- true

  p.ErrorDataReceived.Add(fun d -> if d.Data <> null then traceError d.Data)
  p.OutputDataReceived.Add(fun d -> if d.Data <> null then trace d.Data)
  p.Start() |> ignore
  
  p.BeginErrorReadLine()
  p.BeginOutputReadLine()     
  
  p.WaitForExit()
    
  p.ExitCode

/// Runs the given process
/// returns if the exit code was 0
let execProcess3 infoAction = execProcessAndReturnExitCode infoAction = 0    

/// Runs the given process
/// returns the exit code
let execProcess2 infoAction silent =
  use p = new Process()
  p.StartInfo.UseShellExecute <- false
  infoAction p.StartInfo
  if silent then
    p.StartInfo.RedirectStandardError <- true
  p.Start() |> ignore    
  let error =
    if silent then
      p.StandardError.ReadToEnd()
    else
      String.Empty
    
  p.WaitForExit()
  if silent && p.ExitCode <> 0 then
    System.Diagnostics.Trace.WriteLine(error)
    
  p.ExitCode  

/// Runs the given process
/// returns the exit code
let ExecProcess infoAction  =
  execProcess2 infoAction redirectOutputToTrace
  
/// sets the environment Settings for the given startInfo
/// existing values will be overrriden
let setEnvironmentVariables (startInfo:ProcessStartInfo) environmentSettings = 
  for key,value in environmentSettings do
    if startInfo.EnvironmentVariables.ContainsKey key then
      startInfo.EnvironmentVariables.[key] <- value
    else
      startInfo.EnvironmentVariables.Add(key, value)
          
/// Runs the given process
/// returns true if the exit code was 0
let execProcess infoAction = ExecProcess infoAction = 0    

/// Adds quotes around the string   
let toParam x = " \"" + x + "\" " 
 
/// Use default Parameters
let UseDefaults = id

/// Searches the given directories for the given file
let findFile dirs file =
  try
    dirs
      |> Seq.map (fun path ->
          let dir = new DirectoryInfo(path)
          if not dir.Exists then "" else
          let fi = new FileInfo(Path.Combine(dir.FullName, file))
          if fi.Exists then fi.FullName else "")
      |> Seq.filter ((<>) "")
      |> Seq.head
  with
  | exn -> failwith <| sprintf "%s not found in %A." file dirs