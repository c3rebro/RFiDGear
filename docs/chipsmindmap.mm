<map version="1.0.1">
<!-- To view this file, download free mind mapping software FreeMind from http://freemind.sourceforge.net -->
<node CREATED="1485376386600" ID="ID_1558573202" LINK="menustructuremindmap.html" MODIFIED="1485379745136">
<richcontent TYPE="NODE"><html>
  <head>
    
  </head>
  <body>
    <p>
      RFiDGear App
    </p>
  </body>
</html>
</richcontent>
<font NAME="Courier New" SIZE="12"/>
<node CREATED="1485376494026" ID="ID_1671068648" MODIFIED="1485378548689" POSITION="right" TEXT="Mifare Classic">
<font NAME="Courier New" SIZE="12"/>
<node CREATED="1485376524765" HGAP="19" ID="ID_657319149" MODIFIED="1485378548674" VSHIFT="4">
<richcontent TYPE="NODE"><html>
  <head>
    
  </head>
  <body>
    <p>
      <font face="Courier New">Quick Check </font>
    </p>
    <p>
      <font face="Courier New" size="2">- try to get read access to all blocks on the Card </font>
    </p>
    <p>
      <font face="Courier New" size="2">- place the result in a database including the sector trailer </font>
    </p>
    <p>
      <font face="Courier New" size="2">- use a comma seperated list of default keys in the settings file</font>
    </p>
  </body>
</html>
</richcontent>
<font NAME="Courier New" SIZE="12"/>
</node>
<node CREATED="1485377479598" ID="ID_1311325347" MODIFIED="1485378548674">
<richcontent TYPE="NODE"><html>
  <head>
    
  </head>
  <body>
    <p>
      Preformat Card
    </p>
    <p>
      <font size="1">- use defaults from settings file to write sectoraccessbits and keys to every card</font>
    </p>
  </body>
</html>
</richcontent>
<font NAME="Courier New" SIZE="12"/>
</node>
<node CREATED="1485377916064" ID="ID_1267511025" MODIFIED="1485378548674">
<richcontent TYPE="NODE"><html>
  <head>
    
  </head>
  <body>
    <p>
      Format Card
    </p>
    <p>
      <font size="1">- use QuickCheck Keys and try to write 0's to all Blocks except sectoraccessbits </font>
    </p>
    <p>
      <font size="1">- try to set transport configuration </font>
    </p>
    <p>
      <font size="1">- if fails, ask what to do or for a different key</font>
    </p>
  </body>
</html>
</richcontent>
<font NAME="Courier New" SIZE="12"/>
</node>
<node CREATED="1485378041271" ID="ID_740466768" MODIFIED="1485378548658">
<richcontent TYPE="NODE"><html>
  <head>
    
  </head>
  <body>
    <p>
      Read to Clipboard (Card/Sector/Block)
    </p>
  </body>
</html>
</richcontent>
<font NAME="Courier New" SIZE="12"/>
</node>
<node CREATED="1485378056161" ID="ID_1144988833" MODIFIED="1485378548658">
<richcontent TYPE="NODE"><html>
  <head>
    
  </head>
  <body>
    <p>
      Write Back from Clipboard (to Card/Sector/Block)
    </p>
  </body>
</html>
</richcontent>
<font NAME="Courier New" SIZE="12"/>
</node>
<node CREATED="1485378165619" ID="ID_16497361" MODIFIED="1485378548643">
<richcontent TYPE="NODE"><html>
  <head>
    
  </head>
  <body>
    <p>
      Modify Sector
    </p>
    <p>
      <font size="1">- perform quickcheck on sector(s) </font>
    </p>
    <p>
      <font size="1">- present result (data, accessbits, keys) </font>
    </p>
    <p>
      <font size="1">- write back modifications of data, access bits and keys </font>
    </p>
  </body>
</html>
</richcontent>
<font NAME="Courier New" SIZE="12"/>
</node>
</node>
<node CREATED="1485376505952" ID="ID_719689919" MODIFIED="1485378548643" POSITION="left" TEXT="Mifare Desfire">
<font NAME="Courier New" SIZE="12"/>
<node CREATED="1485376515132" ID="ID_1014039549" MODIFIED="1485379196504">
<richcontent TYPE="NODE"><html>
  <head>
    
  </head>
  <body>
    <p>
      Quick Check
    </p>
    <p>
      <font size="1">- read appid</font>
    </p>
  </body>
</html>
</richcontent>
<font NAME="Courier New" SIZE="12"/>
</node>
<node CREATED="1485379204311" ID="ID_663542361" MODIFIED="1485379701542">
<richcontent TYPE="NODE"><html>
  <head>
    
  </head>
  <body>
    <p>
      Authenticate
    </p>
    <p>
      <font size="1">- to Master Application </font>
    </p>
    <p>
      <font size="1">- to Application ID</font>
    </p>
  </body>
</html>
</richcontent>
<font NAME="Courier New" SIZE="12"/>
</node>
<node CREATED="1485379233631" ID="ID_1718783698" MODIFIED="1485379701527">
<richcontent TYPE="NODE"><html>
  <head>
    
  </head>
  <body>
    <p>
      Preformat Card
    </p>
    <p>
      <font size="1">- create Applications and Files from Template in the Settingsfile</font>
    </p>
  </body>
</html>
</richcontent>
<font NAME="Courier New" SIZE="12"/>
</node>
<node CREATED="1485379356076" ID="ID_1122805347" MODIFIED="1485379701527">
<richcontent TYPE="NODE"><html>
  <head>
    
  </head>
  <body>
    <p>
      Format Card
    </p>
    <p>
      <font size="1">- Delete all Applications using the Master Card Key (not stored anywhere)</font>
    </p>
  </body>
</html>
</richcontent>
<font NAME="Courier New" SIZE="12"/>
</node>
<node CREATED="1485379455560" ID="ID_1366316281" MODIFIED="1485379701511">
<richcontent TYPE="NODE"><html>
  <head>
    
  </head>
  <body>
    <p>
      Read to Clipboard
    </p>
    <p>
      <font size="1">- Authenticate to Application </font>
    </p>
    <p>
      <font size="1">- read Filelist </font>
    </p>
    <p>
      <font size="1">- present filelist and read the selected file(s) to clipboard</font>
    </p>
  </body>
</html>
</richcontent>
<font NAME="Courier New" SIZE="12"/>
</node>
<node CREATED="1485379592669" ID="ID_306037307" MODIFIED="1485379701511" TEXT="Write Back from Clipboard">
<font NAME="Courier New" SIZE="12"/>
</node>
</node>
<node CREATED="1485377511648" ID="ID_404460840" MODIFIED="1485378548643" POSITION="right" TEXT="Edit Default Settings">
<font NAME="Courier New" SIZE="12"/>
<node CREATED="1485378577478" ID="ID_402030206" MODIFIED="1485379701511" TEXT="Edit QuickCheck Keys">
<font NAME="Courier New" SIZE="12"/>
</node>
<node CREATED="1485378590996" ID="ID_833687607" MODIFIED="1485379701511" TEXT="Edit Preformat Templates">
<font NAME="Courier New" SIZE="12"/>
</node>
<node CREATED="1485378654511" ID="ID_1667895806" MODIFIED="1485379701511">
<richcontent TYPE="NODE"><html>
  <head>
    
  </head>
  <body>
    <p>
      Edit Default CardReader
    </p>
  </body>
</html>
</richcontent>
<font NAME="Courier New" SIZE="12"/>
</node>
</node>
</node>
</map>
