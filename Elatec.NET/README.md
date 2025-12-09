# Elatec.NET [![Codacy Badge](https://app.codacy.com/project/badge/Grade/f219c0fa9a484f4580085734c97cba85)](https://app.codacy.com/gh/c3rebro/Elatec.NET/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)
 Elatec TWN4 SimpleProtocol Wrapper for .NET
-
Reader-Shop: https://www.elatec-shop.de/de/

DEV-Kit: https://www.elatec-rfid.com/int/twn4-dev-pack

The TWN4 device needs to be prepared first: In order to use a an Elatec TWN4 Reader, the "Simple Protocol" firmware from the devkit needs to be flashed to the device. Make sure you have a backup of the previous installed firmware if you have a custom one installed on it.
* Download the DEV-Kit
* In "AppBlaster" choose "Program Firmware Image" and use "TWN4_x_y_Multi_CDC_Simple_Protocol.bix"

Tested devices:
* TWN4/B1.06/CCB4.51/PRS1.04/5 (TWN4 Multitec 2 Legic HF LF)
* TWN4/B1.50/NCB4.51/PRS1.04/5 (TWN4 Multitec Legic 45)
* TWN4/B1.06/CCF4.51/PRS1.04   (TWN4 Multitec)

Tested bix file versions:
* 3.22
* 4.50
* 4.51

Hint: Some readers may show unexpected behavior. Especially with Legic Capable "TWN4 Multitec (2) HF LF Legic". The reason is that some ISO14443 commands are executed by the internal legic chip.

Examples for the TWN4 - Legic reader "specialties":
* SelectTag() is only supported by native readers without legic chip, SearchTag() must be used in order to select a Tag.
* RATS command cannot be called manually on the Legic capable readers. Instead it is already executed internally when SearchTag() was called. This will be faced when trying to execute RATS with ISO14443-3_TXD. Use ISO14443A_GetAtsAsync() istead. The reader will deal with the right procedure.
* MifareClassic_LoginAsync() needs the sectornumber. Elatec has a special calculating algorithm for the expected sectornumber. Every sector above sec32 (mifare 4k) is 4times bigger that the lower sectors. They expect the sectornumber also to be timed by 4. So the sectornumber 33 is (33 – 32) * 4 + 32 = 36 dec. Sector 38 is (38 – 32) * 4 + 32 = 56 dec and so on.

> Demo Project: [Elatec.Net.SampleApp](https://github.com/c3rebro/Elatec.Net.SampleApp)

usage example:

    using Elatec.NET;
    using Elatec.NET.Cards;
    using Elatec.NET.Cards.Mifare;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;

    namespace ElatecNetSampleApp
    {
        internal class Program
        {
            static async Task Main(string[] args)
            {
                var reader = TWN4ReaderDevice.Instance.FirstOrDefault();

                if (await reader.ConnectAsync())
                {
                    BaseChip chip = new BaseChip();

                    await reader.BeepAsync(100, 1500, 500, 100);
                    await reader.LedInitAsync();
                    await reader.LedBlinkAsync(Leds.All, 100, 300);

                    await reader.SetTagTypesAsync(LFTagTypes.NOTAG, HFTagTypes.AllHFTags);
                    chip = await reader.GetSingleChipAsync();

                    if(chip != null)
                    {
                        Console.WriteLine("CardType: {0}, UID: {1}, Multitype: ", Enum.GetName(typeof(ChipType), chip.ChipType), chip.UIDHexString);

                        switch (chip.ChipType)
                        {
                            case ChipType.MIFARE:

                                Console.WriteLine("Play Melody? 1: Saints, 2: CamptownRaces");
                                switch(Console.ReadLine())
                                {
                                    case "1":
                                        await reader.PlayMelody(90, MySongs.OhWhenTheSaints);
                                        break;
                                    default:
                                        break;

                                }
                            

                                MifareChip mifareChip = (MifareChip)chip;

                                Console.WriteLine("\nFound: {0}\n", mifareChip.SubType);

                                // bitfield-filter on all desfire type chips (EV1 4k, EV2 32k, SmartMX-Desfire etc.)
                                switch (mifareChip.SubType & MifareChipSubType.DESFire)
                                {
                                    case MifareChipSubType.DESFire:
                                        if(reader.IsTWN4LegicReader)
                                        {
                                            // undocumented in elatec's devkit (as of customersupport): if the Reader is a TWN4 Multitec with LEGIC capabilities.
                                            // SelectTag is not working. Instead, a SearchTag must be used. The SelectTag is then executed internally.
                                            if(reader.IsTWN4LegicReader)
                                            {
                                                await reader.SearchTagAsync();
                                            }
                                        }
                                        else
                                        {
                                            await reader.ISO14443A_SelectTagAsync(chip.UID);
                                        }

                                        try
                                        {
                                            var appIDs = await reader.MifareDesfire_GetAppIDsAsync();

                                            foreach (var appID in appIDs)
                                            {
                                                Console.WriteLine("Found AppID(s): {0}", appID.ToString("X8"));
                                            }
                                        
                                            //try to create app, auth on picc first
                                            await reader.MifareDesfire_SelectApplicationAsync(0);
                                            await reader.MifareDesfire_CreateApplicationAsync(
                                                DESFireAppAccessRights.KS_DEFAULT,
                                                DESFireKeyType.DF_KEY_AES,
                                                1,
                                                0x3060);
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine("\nErr: {0}\n", e.Message);
                                        }
                                        break;
                                }
                                Console.ReadLine();
                                break;

                            default:
                                Console.WriteLine("Chip Found: {0}", Enum.GetName(typeof(ChipType), chip.ChipType));
                                Console.ReadLine();
                                break;
                        }
                    }

                    else
                    {
                        await reader.DisconnectAsync();
                    }
                }  
            }
        }

        static class MySongs
        {
            public static List<TWN4ReaderDevice.Tone> OhWhenTheSaints
            {
                get => new List<TWN4ReaderDevice.Tone>()
                {
                    new TWN4ReaderDevice.Tone() { Value = 4,  Volume = 0, Pitch = NotePitch.PAUSE },
                    new TWN4ReaderDevice.Tone() { Value = 4,  Pitch = NotePitch.C3 },
                    new TWN4ReaderDevice.Tone() { Value = 4,  Pitch = NotePitch.E3 },
                    new TWN4ReaderDevice.Tone() { Value = 4,  Pitch = NotePitch.F3 },

                    new TWN4ReaderDevice.Tone() { Pitch = NotePitch.G3 },

                    new TWN4ReaderDevice.Tone() { Value = 4,  Volume = 0, Pitch = NotePitch.PAUSE },
                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.C3 },
                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.E3 },
                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.F3 },

                    new TWN4ReaderDevice.Tone() { Pitch = NotePitch.G3 },
                    // 1
                    new TWN4ReaderDevice.Tone() { Value = 4,  Volume = 0, Pitch = NotePitch.PAUSE },
                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.C3 },
                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.E3 },
                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.F3 },

                    new TWN4ReaderDevice.Tone() { Value = 8, Pitch = NotePitch.G3 },
                    new TWN4ReaderDevice.Tone() { Value = 8, Pitch = NotePitch.E3 },

                    new TWN4ReaderDevice.Tone() { Value = 8, Pitch = NotePitch.C3 },
                    new TWN4ReaderDevice.Tone() { Value = 8, Pitch = NotePitch.E3 },

                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.E3 },
                    new TWN4ReaderDevice.Tone() { Value = 12, Pitch = NotePitch.D3 },
                    // 2
                    new TWN4ReaderDevice.Tone() { Value = 4,  Volume = 0, Pitch = NotePitch.PAUSE },
                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.E3 },
                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.E3 },
                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.D3 },

                    new TWN4ReaderDevice.Tone() { Value = 12, Pitch = NotePitch.C3 },
                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.C3 },

                    new TWN4ReaderDevice.Tone() { Value = 8, Pitch = NotePitch.E3 },
                    new TWN4ReaderDevice.Tone() { Value = 8, Pitch = NotePitch.G3 },

                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.G3 },
                    new TWN4ReaderDevice.Tone() { Value = 12, Pitch = NotePitch.F3 },
                    // 3
                    new TWN4ReaderDevice.Tone() { Value = 4,  Volume = 0, Pitch = NotePitch.PAUSE },
                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.F3 },
                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.E3 },
                    new TWN4ReaderDevice.Tone() { Value = 4, Pitch = NotePitch.F3 },

                    new TWN4ReaderDevice.Tone() { Value = 8, Pitch = NotePitch.G3 },
                    new TWN4ReaderDevice.Tone() { Value = 8, Pitch = NotePitch.E3 },

                    new TWN4ReaderDevice.Tone() { Value = 8, Pitch = NotePitch.C3 },
                    new TWN4ReaderDevice.Tone() { Value = 8, Pitch = NotePitch.D3 },

                    new TWN4ReaderDevice.Tone() { Pitch = NotePitch.C3 }
                    // 4
                };
            }
        }
    }

## Contributors ✨

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>  
    <tbody>    
        <tr>      
            <td align="center" valign="top" width="14.28%">        
                <a href="https://github.com/c3rebro">
                    <img src="https://avatars.githubusercontent.com/u/5468524?v=4?s=100" width="100px;" alt="Steven (c3rebro)"/><br />        
                    <sub><b>Steven (c3rebro)</b></sub></a><br />        
                <a href="#code" title="Code">💻</a> 
            <td align="center" valign="top" width="14.28%">
                <a href="https://github.com/faiteanu">
                    <img src="https://avatars.githubusercontent.com/u/63024793?v=4?s=100" width="100px;" alt="Fabian Aiteanu"/><br />      
                    <sub><b>Fabian Aiteanu</b></sub></a><br />
                <a href="#code" title="Code">💻</a> 
           <td align="center" valign="top" width="14.28%">
                <a href="https://github.com/Xentrice">
                    <img src="https://avatars.githubusercontent.com/u/12209220?v=4?s=100" width="100px;" alt="Sebastian Felber"/><br />      
                    <sub><b>Sebastian Felber</b></sub></a><br />
                <a href="#code" title="Code">💻</a>
        </tr>  
    </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->
