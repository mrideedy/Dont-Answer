# Don't Answer - voice script

Generate each line with the character's voice and save it as **`<ID>.mp3`** into `Assets/_Game/Resources/Voice/`. The file name must match the ID exactly.

**Settings:** normal lines use Stability ~45%, Similarity ~80%, Style ~25%. Lines marked **MIMIC** use the *same voice* with Stability ~10% and Style 0%, which makes the delivery uncanny. The game adds the rest of the creepiness. Text like `--` means the line cuts off abruptly.

## VOSS
*Voice: Elderly man in his 70s. Gravelly, tired, slow small-town American. Has seen terrible things and doesn't talk about them.*

| ID | Type | Line | Direction |
|---|---|---|---|
| `VOSS_P01` |  | You must be the Hollis kid. | Weary. A small pause before 'Hollis', like the name weighs something. |
| `VOSS_P02` |  | I knew your brother. Everybody in Hollow Creek knew Leo. | Quiet, sincere, looking away. |
| `VOSS_P03` |  | I'll be honest. I don't know why anyone would want this place. Not tonight. | Uneasy, almost apologetic. |
| `VOSS_P04` |  | Front door, back door. The back bolt sticks, so lean on it. | Practical, handing over keys. |
| `VOSS_P05` |  | The rules are on the wall by the door. My father wrote them. His father before him. Read every word. | Firm and grave. Slow on 'every word'. |
| `VOSS_P06` |  | And kid... whatever knocks tonight, it isn't always who it says it is. | Low, close, almost a whisper. |
| `VOSS_P07` |  | I'll see you at sunrise. Sunrise. Not before. | Walking away, turns back for the last words. Insistent. |
| `VOSS_E01` |  | Sun's up, kid. It's over. | Morning. Tired relief. |
| `VOSS_E02` |  | You did good. Better than most. | Gentle, proud. |
| `VOSS_E03` |  | Well, I'll be damned... Leo Hollis. | Stunned, voice catching. True ending. |
| `VOSSF_K1` | **MIMIC** | It's Voss. Open up. | Flat. |
| `VOSSF_K2` | **MIMIC** | Sun's up, kid. It's over. | Too cheerful. |
| `VOSSF_K3` | **MIMIC** | It's Voss. Your landlord. | Flat. |
| `VOSSF_A1` | **MIMIC** | Voss. Your landlord. I gave you the keys, remember? | Smiling voice. |
| `VOSSF_A2` | **MIMIC** | It's over. Sun's up. Open the door so I can check on the place. | Easy. |
| `VOSSF_A3` | **MIMIC** | Six. It's six o'clock. Sunrise. Look out your window. It's over. | Insistent. |
| `VOSSF_A4` | **MIMIC** | Rule seven is... Rule seven... Open the door, kid. | Can't find it. |
| `VOSSF_GA` | **MIMIC** | Don't be stupid, kid. It's over. | Annoyed. |
| `VOSSF_AN` | **MIMIC** | IT'S OVER! IT'S OVER! OPEN THE DOOR! | Screaming. |
| `VOSSF_L` | **MIMIC** | ...So close. | Whisper. |
| `VOSSF_D` | **MIMIC** | Thank you, kid. | Whisper. |

## LEO
*Voice: Boy around 9 years old. Soft, sweet, a little shy. If you can't find a child voice, pick the youngest, softest voice available; the game can raise its pitch slightly.*

| ID | Type | Line | Direction |
|---|---|---|---|
| `MEM_PHOTO` | memory | Sammy, look! I'm a ghost! Wooooo... You cut the eye holes crooked. | Excited 9-year-old, giggling. A happy memory. |
| `MEM_RADIO` | memory | Sam, come in, Sam. Over... Are you still mad at me? Over. | Small voice, as if through a walkie-talkie. Hopeful, then unsure. |
| `MEM_KNOCK` | memory | If you're ever scared, knock our knock. Two fast, one slow, two fast. And I'll always open. Promise? ...Pinky promise. | Earnest and sweet. Very important line - say the knock rhythm clearly. |
| `MEM_RABBIT` | memory | Mr. Buttons doesn't like the dark either. Can he sleep in your room? Just tonight? | Sleepy, hopeful, a little embarrassed. |
| `MEM_ASK` | memory | Sam? Can you come trick-or-treating with me? Mom said you have to. ...Please? | Through a closed bedroom door. Shy, hopeful. |
| `MEM_OKAY` | memory | ...Okay. I'll go by myself. | Hurt, quiet. The last thing he said to Sam in person. |
| `MEM_VOICEMAIL` | memory | Sam? Sam, I'm on the porch of the big house on Elm. I can't find the others... it's really dark. Can you come get me? ...Sam? Someone's coming. It looks like Mom. Sam, it looks like Mom, but it's-- | Crying, scared, rain around him. Cut off abruptly mid-word. |
| `MEM_LIGHTHOUSE` | memory | When I grow up, I'm gonna be a lighthouse keeper. So nobody ever gets lost in the dark. | Dreamy, proud. |
| `LEOF_K1` | **MIMIC** | Sam? | Leo's voice, but too steady. Not scared at all. |
| `LEOF_K2` | **MIMIC** | Sammy, it's me. It's Leo. | Same. |
| `LEOF_K3` | **MIMIC** | I found my way home, Sam. | Slow, smiling. |
| `LEOF_A1` | **MIMIC** | It's me. Leo. Your brother. Don't you recognize me? | Flat sweetness. |
| `LEOF_A2` | **MIMIC** | Somewhere dark. It's so cold there, Sam. Everyone's there. Mom's there too. | Dreamy, wrong. |
| `LEOF_A3` | **MIMIC** | Our... knock? Knock knock. Knock knock. Knock knock knock knock-- | Starts unsure, escalates into frantic chanting. |
| `LEOF_A4` | **MIMIC** | A ghost. I wanted to be a ghost. And now I am. | Delighted. |
| `LEOF_GA` | **MIMIC** | You said that last time. | Dead flat. The cruelest line in the game. |
| `LEOF_AN` | **MIMIC** | YOU SAID THAT LAST TIME! OPEN THE DOOR! | Screaming, distorted. |
| `LEOF_W1` | **MIMIC** | Sam? I'm at your window. | Whispered against glass. |
| `LEOR_K1` |  | Sam? ...Sammy? It's me. | Small, shivering, unsure if anyone is there. |
| `LEOR_A1` |  | It's Leo. I don't know how long I was gone. It felt like one night. | Confused, tired. |
| `LEOR_A2` |  | Two fast, one slow, two fast. I just did it! You promised you'd always open. | Hopeful, a little hurt. |
| `LEOR_A3` |  | A lighthouse keeper. So nobody gets lost in the dark. Like I did. | Quiet. |
| `LEOR_A4` |  | I know. I heard you. Every night, I heard you saying sorry. That's how I found the way back. | Gentle, forgiving. |
| `LEOR_GA` |  | ...Okay. Okay, Sam. I'll go away. | Heartbroken. |
| `LEOR_LI` |  | Sam! | Crying with relief. |
| `LEOR_E1` |  | Can we go home now? | Sleepy, safe. |
| `LEOX_K1` | **MIMIC** | Sam? ...Sammy? It's me. | Almost perfect. Almost. |
| `LEOX_A1` | **MIMIC** | It's Leo. I was gone so long. So long, Sam. | Flat. |
| `LEOX_A2` | **MIMIC** | Our knock? Knock... knock. Knock. Right? Right, Sam? | Guessing. |
| `LEOX_A2B` | **MIMIC** | Two fast, one slow, two fast. I just did it. You promised. | Too fast, too eager. Used when they learned the knock. |
| `LEOX_A3` | **MIMIC** | A... ghost. I wanted to be a ghost. Like now. | Pleased. |
| `LEOX_A4` | **MIMIC** | Sorry doesn't open doors, Sam. | Cold. |
| `LEOX_GA` | **MIMIC** | Then I'll wait. I'm good at waiting now. | Calm. |
| `LEOX_AN` | **MIMIC** | SAMMY. SAMMY. SAMMY. OPEN. | Screaming. |
| `SLOT_01` | **MIMIC** | Sam... Sammy... let me in... | Whispered through a mail slot. |
| `LEOF_D` | **MIMIC** | Thank you, Sammy. | Sweet whisper. |
| `LEOX_D` | **MIMIC** | Now we can play. | Giggling whisper. |
| `REPL_LEO` | **MIMIC** | Hello? ...It's Sam. Let me in. | Same, whispered. |

## MOM
*Voice: Woman in her 50s. Exhausted, tearful, loving.*

| ID | Type | Line | Direction |
|---|---|---|---|
| `MOM_CALL1` |  | Sam... it's Mom. I know you're there. I'm not angry. I just-- I can't lose you too. Please come home in the morning. Please. | Tearful, exhausted, leaving a message. |
| `HOME_01` | **MIMIC** | Sammy? It's Mom, honey. I'm right outside. It's so cold out here. | Sweet. Too sweet. On the phone. |
| `HOME_02` | **MIMIC** | Do our knock for me, sweetheart. Leo's knock. I forgot how it goes. | Slower, wrong. |
| `HOME_03` | **MIMIC** | ...two fast... one slow... two fast. Thank you, Sam. | Whispered, many voices at once. |

## ANCHOR
*Voice: Male local TV news anchor, 50s, 1990s broadcast style. Measured and professional.*

| ID | Type | Line | Direction |
|---|---|---|---|
| `NEWS_01` |  | ...marking one year since nine-year-old Leo Hollis disappeared from Elm Street on Halloween night. Since nineteen fifty-two, Hollow Creek has reported fourteen disappearances on October thirty-first. | 1990s small-town TV anchor. Measured, professional. |
| `NEWS_02` |  | Residents are advised to remain indoors until sunrise, and to keep all doors and windows locked. | Same, slightly slower. |
| `NEWS_03` | **MIMIC** | If you are watching this... do not open your door. Do not open your door. Do not open your door. | Starts professional, becomes flat and mechanical with each repeat. |
| `NEWS_04` | **MIMIC** | Sam. We can see you. Turn around, Sam. | Very slow, intimate, speaking straight into the camera. |

## MARCO
*Voice: Young man in his 20s. Friendly pizza driver, slightly nervous, New Jersey / Italian-American flavor.*

| ID | Type | Line | Direction |
|---|---|---|---|
| `MARCO_K1` |  | Gino's Pizza! | Cheerful, raising his voice through a door. |
| `MARCO_K2` |  | Pizza for apartment three! | Same. |
| `MARCO_K3` |  | Hello? Your pizza's gettin' cold, pal. | A bit impatient but friendly. |
| `MARCO_A1` |  | Marco, from Gino's. Got a large pepperoni with your name on it. | Friendly. |
| `MARCO_A2` |  | Just to hand this off and get back in my car. No offense, but this street gives me the creeps tonight. | Nervous laugh. |
| `MARCO_A3` |  | Uh, lemme check... thirty-one. Order thirty-one. Already paid. | Reading off a receipt. |
| `MARCO_A4` |  | Yeah, there's a guy standing by the fence across the street. Just... standing there. Facing your house. | Unsettled, lowering his voice. |
| `MARCO_GA` |  | Alright, alright. I'll leave it on the step. | Shrugging it off. |
| `MARCO_L` |  | Leaving it on the step! Enjoy! | Walking away. |
| `MARCO_LI` |  | Here ya go. Hey, take my flashlight too, the streetlights keep cutting out. Stay safe, okay? | Warm, relieved. |
| `MARCO_TK` |  | Hey-- who's there? HEY! Get off me-- GET OFF-- | Panicked, from a distance outside. Cut off. |
| `MARCOM_K1` | **MIMIC** | Gino's Pizza. | Flat. A beat too slow. Like a recording of a person. |
| `MARCOM_K2` | **MIMIC** | Pizza. For apartment three. Three. | Same. |
| `MARCOM_A1` | **MIMIC** | Marco. From Gino's. Got a large pepperoni with your name on it. Your name. Sam. | Friendly words, no warmth. Lands hard on 'Sam'. |
| `MARCOM_A2` | **MIMIC** | I want to come in. It's cold. The pizza is cold. I am cold. | Monotone. |
| `MARCOM_A3` | **MIMIC** | Thirty... thirteen. Thirteen. It's paid. It's already paid. | Searching for the right answer and failing. |
| `MARCOM_A4` | **MIMIC** | Just you. I can see you. Through the little hole. | Whispered, close to the door. |
| `MARCOM_GA` | **MIMIC** | I'll wait on the step. | Flat. |
| `MARCOM_AN` | **MIMIC** | OPEN THE DOOR. THE PIZZA IS COLD. | Screaming, raw. |
| `MARCOM_L` | **MIMIC** | ...Next time. | Whisper. |
| `MARCOM_D` | **MIMIC** | Thank you, Sam. | Soft, intimate, right in your face. The last thing you hear. |

## KIDS
*Voice: A child's voice (any). The game layers it three times so it sounds like children speaking in unison.*

| ID | Type | Line | Direction |
|---|---|---|---|
| `MASKS_K1` | **MIMIC** | Trick or treat. | Children speaking in perfect unison. Flat, no excitement. |
| `MASKS_K2` | **MIMIC** | Trick or treat. Trick or treat. Trick or treat. | Same, perfectly even rhythm. |
| `MASKS_K3` | **MIMIC** | Smell our feet. Give us something good to eat. | Sing-song, but too slow. |
| `MASKS_A1` | **MIMIC** | We're trick-or-treaters. | Unison, flat. |
| `MASKS_A2` | **MIMIC** | Treats. Open the door and give us treats. | Unison. |
| `MASKS_A3` | **MIMIC** | ...Inside. They're inside. With you. | After a long pause. Whispered. |
| `MASKS_A4` | **MIMIC** | These aren't masks. | Then a giggle. |
| `MASKS_GA` | **MIMIC** | Then we pick trick. | Giggling. |
| `MASKS_AN` | **MIMIC** | TRICK. TRICK. TRICK. TRICK. | Shrieking. |
| `MASKS_D` | **MIMIC** | Thank you for inviting us. | Unison whisper. |

## ANA
*Voice: Woman in her late 20s. Warm, sarcastic, slightly husky. Sam's best friend.*

| ID | Type | Line | Direction |
|---|---|---|---|
| `ANA_K1` |  | Sam? It's me! | Calling through the door, relieved to have arrived. |
| `ANA_K2` |  | Open up, I'm soaked! | Laughing at herself, shivering. |
| `ANA_K3` |  | Sam, I can see your light on! | Exasperated, fond. |
| `ANA_A1` |  | It's Ana! Your best friend? Drove four hours to babysit you on the worst night of your life? | Sarcastic and warm. |
| `ANA_A2` |  | My car died on Route 9. I texted you! Please, I'm freezing out here. | Teeth chattering. |
| `ANA_A3` |  | Banana. He called me Banana-Ana. Every single time. God, I miss that kid. | Starts with a laugh, voice cracks at the end. |
| `ANA_A4` |  | Because it's one year tonight, and you shouldn't be alone. That's why. | Soft, serious. |
| `ANA_GA` |  | Are you serious? ...Okay. Okay, Sam. I'll walk back to the car. | Hurt, trying not to show it. |
| `ANA_L` |  | I hope you know what you're doing. | Walking away in the rain. |
| `ANA_LI` |  | Oh, thank God. Come here. | Relieved, pulling Sam into a hug. |
| `ANA_TK` |  | Sam?! SAM! OPEN THE DOOR! SAM-- | Pure terror from outside. Cut off. |
| `ANA_IN1` |  | That's not me, Sam. That is NOT me. | Hushed, terrified. She's inside, staring at the door. |
| `ANA_IN2` |  | Sam... don't. Look at me. Don't. | Pleading, quiet. |
| `ANA_IN3` |  | It's still dark out. The sun's not up, Sam. | Urgent whisper. |
| `ANA_IN4` |  | Do you think he's out there? Leo? | Quiet, sitting on the couch. |
| `ANA_IN5` |  | I keep hearing scratching. In the walls. | Uneasy. |
| `ANAM_K1` | **MIMIC** | Sam? It's me. | Right words, wrong music. Too calm. |
| `ANAM_K2` | **MIMIC** | Open up. I'm soaked. I'm soaked. I'm soaked. | Repeating like a skipping record. |
| `ANAM_A1` | **MIMIC** | It's Ana. Your best friend. Your best friend, Sam. | Flat. |
| `ANAM_A2` | **MIMIC** | My car died. I walked. I walked so far to get here, Sam. Let me in. | Slow, hollow. |
| `ANAM_A3` | **MIMIC** | Leo called me... Leo... He's here, Sam. He's right behind me. Open the door and see. | Losing the memory, then a smile in the voice. |
| `ANAM_A4` | **MIMIC** | Because you're alone. You're always alone now. | Cruel and gentle at once. |
| `ANAM_GA` | **MIMIC** | You don't mean that. | Flat. |
| `ANAM_AN` | **MIMIC** | SAM. OPEN THE DOOR, SAM. | Screaming. |
| `ANAM_L` | **MIMIC** | ...I'll be at the back door. | Whisper. |
| `ANAM_D` | **MIMIC** | Thank you, Sam. | Soft, intimate. |
| `REPL_ANA` | **MIMIC** | Hello? ...It's Sam. Let me in. | Whispered from outside a door. Used layered with other voices. |

## OFFICER
*Voice: Man in his 40s. Deep, calm, flat, authoritative police voice.*

| ID | Type | Line | Direction |
|---|---|---|---|
| `OFFH_K1` |  | Hollow Creek Police. Welfare check. | Calm, authoritative, through a door. |
| `OFFH_K2` |  | Police. Anybody home? | Same. |
| `OFFH_K3` |  | This is the police. Just checking on you. | Softer. |
| `OFFH_A1` |  | Officer Daniels, Hollow Creek PD. We're checking on everyone on Elm tonight. | Professional, tired. |
| `OFFH_A2` |  | Just making sure you're safe. Honestly? Don't open up. Keep it locked till morning. | Sincere. |
| `OFFH_A3` |  | Four-four-seven-one. Call dispatch if you want to check. | Patient. |
| `OFFH_A4` |  | ...I was on the search team last year. I'm sorry. We never stopped looking for him. | Heavy, genuine. |
| `OFFH_GA` |  | Good. Smart. Stay inside till sunrise. | Approving. |
| `OFFH_L` |  | Stay safe in there. | Walking off. |
| `OFFH_LI` |  | You really shouldn't have opened that... but thank you. I'll keep watch on the back door. | Surprised, then grateful. |
| `OFFH_TK` |  | Show me your hands-- I said SHOW ME YOUR-- oh God-- | Shouting from outside, then terror. |
| `OFFH_IN1` |  | Something's at the back door. Stay behind me. | Low, steady. |
| `OFFM_K1` | **MIMIC** | Hollow Creek Police. Open up. | Flat. |
| `OFFM_K2` | **MIMIC** | Police. Open the door. | Flat. |
| `OFFM_K3` | **MIMIC** | Open the door, please. | Too polite. |
| `OFFM_A1` | **MIMIC** | Officer Daniels. Hollow Creek Police Department. | Recited. |
| `OFFM_A2` | **MIMIC** | We are checking every home on Elm Street. I need to come inside. I need to come inside. | Repeats exactly. |
| `OFFM_A3` | **MIMIC** | My badge number is... Daniels. Officer Daniels. Open the door, please. | Confident about the wrong thing. |
| `OFFM_A4` | **MIMIC** | Yes. He is with us. Open the door and I will take you to him. | Soft. Inviting. |
| `OFFM_GA` | **MIMIC** | That was not a request. | Cold. |
| `OFFM_AN` | **MIMIC** | OPEN. THE. DOOR. | Each word a scream. |
| `OFFM_L` | **MIMIC** | We'll be back. | Whisper. |
| `OFFM_D` | **MIMIC** | Thank you for your cooperation. | Formal and close. |

## TOM
*Voice: Young man in his 20s. Goofy, cheerful, a bit drunk.*

| ID | Type | Line | Direction |
|---|---|---|---|
| `TOMH_K1` |  | Neighbor! Heyyy, neighbor! | Tipsy and cheerful. |
| `TOMH_K2` |  | It's Tom! From upstairs! | Same. |
| `TOMH_K3` |  | Knock knock! ...It's Tom. | Giggling at his own joke. |
| `TOMH_A1` |  | Tom! Two-B! We met at the mailboxes this morning, you had all those boxes! | Slightly slurred, friendly. |
| `TOMH_A2` |  | Lost my keys. Again. Can I crash on your couch? I texted you! | Pleading puppy. |
| `TOMH_A3` |  | It's my costume! I'm a skeleton. Spooky! ...Wait. You thought I was one of THEM? Ha. Ha... that's not funny. | Laughs, then sobers up fast. |
| `TOMH_A4` |  | The fuse box? Ancient. If the power goes, flip the big red one. I'm basically the building's handyman. | Proud. |
| `TOMH_GA` |  | Aw, man... guess I'm sleeping in the stairwell. | Disappointed. |
| `TOMH_L` |  | Stairwell it is... | Trailing off. |
| `TOMH_LI` |  | You're a lifesaver! Dibs on the armchair. | Delighted. |
| `TOMH_TK` |  | Who's down there? ...Hello? ...Oh God. OH GOD-- | From the stairwell, echoing. Cut off. |
| `TOMH_IN1` |  | Mrs. Hale? She died last year. Don't open it. DON'T. | Instantly sober and scared. |
| `TOMH_IN2` |  | Power's out? I got this. Stay here, I know that basement. | Trying to sound brave. |
| `TOMH_IN3` |  | ...Power's back! Told ya. Handyman. | Out of breath, relieved. |
| `TOMM_K1` | **MIMIC** | Neighbor. Hey. Neighbor. | Cheerful words, dead voice. |
| `TOMM_K2` | **MIMIC** | It's Tom. From upstairs. Upstairs. | Same. |
| `TOMM_A1` | **MIMIC** | Tom. Two-B. We met at the mailboxes. I watched you. | Flat. |
| `TOMM_A2` | **MIMIC** | Lost my keys. Let me in. Let me sleep in your house. | Slow. |
| `TOMM_A3` | **MIMIC** | What mask? | Genuinely confused. Chilling. |
| `TOMM_A4` | **MIMIC** | It's dark in the basement. That's where we wait. | Whisper. |
| `TOMM_GA` | **MIMIC** | But we're neighbors. | Flat. |
| `TOMM_AN` | **MIMIC** | NEIGHBOR. NEIGHBOR. NEIGHBOR. | Screaming. |
| `TOMM_L` | **MIMIC** | I'll be right upstairs. | Whisper. |
| `TOMM_D` | **MIMIC** | Thanks, neighbor. | Whisper. |

## ELI
*Voice: Man in his 40s. Breathless, panicked, in pain.*

| ID | Type | Line | Direction |
|---|---|---|---|
| `ELI_K1` |  | Please! Somebody! | Panicked, out of breath, banging. |
| `ELI_K2` |  | Help me, please, I'm hurt! | Same, in pain. |
| `ELI_K3` |  | Is anyone there?! PLEASE! | Desperate. |
| `ELI_A1` |  | Eli. Eli Brandt. I live on Birch Lane, two streets over. Please... | Gasping. |
| `ELI_A2` |  | Something attacked me. I'm bleeding. I just need to get off the street! | Frantic. |
| `ELI_A3` |  | It had my wife's face. It had her FACE. She's been dead two years... | Breaking down. |
| `ELI_A4` |  | A boy in a sheet? Yes! Behind the church, an hour ago. He was crying for someone named Sam. | Urgent, remembering. |
| `ELI_GA` |  | Please! Please don't do this! | Begging. |
| `ELI_L` |  | ...Okay. Okay. I'll keep running. | Defeated, limping off. |
| `ELI_LI` |  | Thank you. God, thank you. Lock it. LOCK IT. | Collapsing inside. |
| `ELI_TK` |  | No-- no no no, it's here-- IT'S HERE-- AAAH-- | Right outside the door. Horrible. |
| `ELI_IN1` |  | It's still dark. Whatever it says... don't. | Weak, from the chair. |
| `ELIM_K1` | **MIMIC** | Please. Somebody. | Eli's words with no panic in them at all. |
| `ELIM_K2` | **MIMIC** | Help me. Please. I'm hurt. | Flat, calm, wrong. |
| `ELIM_A1` | **MIMIC** | Eli. Eli Brandt. Birch Lane. I'm better now. | Calm and pleased. |
| `ELIM_A2` | **MIMIC** | I'm not bleeding anymore. Let me in and see. | Gentle. |
| `ELIM_A3` | **MIMIC** | It had my wife's face. Now I have mine. | Slow smile in the voice. |
| `ELIM_A4` | **MIMIC** | The boy? He's with us now. He's waiting for you. | Whisper. |
| `ELIM_GA` | **MIMIC** | Please. Please. Please. | Same pitch every time. |
| `ELIM_AN` | **MIMIC** | LET ME IN! I'M BETTER NOW! | Screaming. |
| `ELIM_L` | **MIMIC** | ...I'll keep running. | Whisper. |
| `ELIM_D` | **MIMIC** | Thank you. | Whisper. |

## DAD
*Voice: Man in his 50s. Rough, gravelly, emotional, a little drunk. A broken father.*

| ID | Type | Line | Direction |
|---|---|---|---|
| `DADH_K1` |  | Sam? Sam, it's Dad. | Rough voice, a little drunk, gentle. |
| `DADH_K2` |  | Please, kiddo. Just open the door. | Pleading. |
| `DADH_K3` |  | I know you're in there. | Tired. |
| `DADH_A1` |  | It's your father. I know... I know I haven't been much of one. Not since. | Ashamed. |
| `DADH_A2` |  | I couldn't sit in that house tonight. I've been walking. Calling his name. Like an idiot. | Bitter laugh. |
| `DADH_A3` |  | A lighthouse keeper. A lighthouse keeper, of all things. So nobody would ever get lost in the dark. | Fond, crying a little. |
| `DADH_A4` |  | Because it wasn't your fault, Sam. It was never your fault. I should've been the one walking him that night. Not you. | The emotional core. Slow, broken. |
| `DADH_GA` |  | ...Okay. I understand. I love you, kiddo. | Quiet acceptance. |
| `DADH_L` |  | I'll keep looking. I'll always keep looking. | Walking away. |
| `DADH_LI` |  | Come here. Come here... | Breaking down, hugging. |
| `DADH_TK` |  | Leo?! Leo, is that you? ...Oh no. No, you're not-- | Hope turning to horror, from outside. |
| `DADM_K1` | **MIMIC** | Sam. It's Dad. | Flat. |
| `DADM_K2` | **MIMIC** | Open the door for your father. | Cold. |
| `DADM_A1` | **MIMIC** | It's your father. Your father. Open the door for your father. | Repetitive. |
| `DADM_A2` | **MIMIC** | I've been walking. Calling his name. Leo. Leo. Leo. He answered, Sam. | Slow and pleased. |
| `DADM_A3` | **MIMIC** | He wanted to be... with us. He wanted to be with us. Now he is. | Wrong. |
| `DADM_A4` | **MIMIC** | Because it WAS your fault, Sam. You told him to go away. | Cruel, quiet. |
| `DADM_GA` | **MIMIC** | Your mother would be so disappointed. | Flat. |
| `DADM_AN` | **MIMIC** | OPEN THE DOOR FOR YOUR FATHER. | Roaring. |
| `DADM_L` | **MIMIC** | I'll tell Leo you said no. Again. | Whisper. |
| `DADM_D` | **MIMIC** | Good kid. | Low, close. |
| `REPL_DAD` | **MIMIC** | Hello? ...It's Sam. Let me in. | Same, whispered. |

## HALE
*Voice: Elderly woman in her 80s. Sweet, sing-song grandmother voice that makes your skin crawl.*

| ID | Type | Line | Direction |
|---|---|---|---|
| `HALE_K1` | **MIMIC** | Yoo-hoo, dear! | Sweet little old lady, sing-song. |
| `HALE_K2` | **MIMIC** | It's Mrs. Hale, dear. From downstairs. | Same. |
| `HALE_K3` | **MIMIC** | Dear. Dear. Dear. Dear. | Same pitch every time. Like a clock. |
| `HALE_A1` | **MIMIC** | Mrs. Hale, dear. From downstairs. We haven't met. But I've watched you all night. | Sweet, with something underneath. |
| `HALE_A2` | **MIMIC** | My cat got out, dear. She's in your apartment. I can hear her in your walls. | Conspiratorial. |
| `HALE_A3` | **MIMIC** | Her name... her name is... Let me in, dear, and I'll tell you. | Losing the thread. |
| `HALE_A4` | **MIMIC** | Oh, the little ghost? I let him in, dear. Last year. He's been with me ever since. | Fond. Horrifying. |
| `HALE_GA` | **MIMIC** | That's not very neighborly, dear. | Disappointed grandmother. |
| `HALE_AN` | **MIMIC** | LET. ME. IN. DEAR. | Shrieking. |
| `HALE_L` | **MIMIC** | I'll be in the walls, dear. | Whisper. |
| `SLOT_02` | **MIMIC** | I can see your feet, dear. | Whispered through a mail slot, delighted. |
| `HALE_D` | **MIMIC** | Thank you, dear. | Delighted whisper. |

## Sound effects (ElevenLabs > Sound Effects)
Save each as **`<id>.mp3`** into `Assets/_Game/Resources/SFX/`. Tick *Loop* where it says loop.

| id | Seconds | Loop | Prompt |
|---|---|---|---|
| `knock_firm` | 2 | no | Three firm knuckle knocks on a solid wooden front door, recorded from inside a quiet house at night |
| `knock_soft` | 2 | no | Soft hesitant knocking on a wooden door by small knuckles, recorded from inside a quiet house |
| `knock_pound` | 3 | no | Violent pounding of fists on a wooden front door, the door shaking and rattling in its frame, recorded from inside |
| `knock_single` | 2 | no | One slow loud knock on a wooden door in a completely silent house |
| `knock_secret` | 3 | no | A rhythmic knock pattern on a wooden door: two quick knocks, a pause, one knock, a pause, two quick knocks |
| `door_creak` | 3 | no | Old wooden front door slowly creaking open, long eerie hinge squeal |
| `door_slam` | 2 | no | Heavy wooden front door slamming shut followed by a deadbolt clicking |
| `doorknob_rattle` | 2 | no | Brass doorknob being turned and rattled from the other side of a locked door |
| `deadbolt` | 1 | no | Old sticky deadbolt lock being forced shut with a heavy metal clunk |
| `window_latch` | 1 | no | Old wooden window being slid shut and its metal latch snapping closed |
| `window_tap` | 3 | no | Fingernails tapping slowly on a window pane, recorded from inside |
| `window_scratch` | 4 | no | Long fingernails scraping slowly down a glass window pane |
| `wall_scratch` | 5 | no | Something scratching and crawling inside the wall of an old house, close and muffled |
| `footsteps_room` | 5 | no | Slow heavy footsteps on creaking wooden floorboards in the next room of an old house |
| `footsteps_ceiling` | 6 | no | Muffled footsteps walking slowly across the ceiling from the apartment above |
| `porch_steps` | 4 | no | Footsteps walking up creaky wooden porch steps in the rain |
| `breath_door` | 6 | yes | Slow wet raspy breathing right on the other side of a door, very close and intimate |
| `whisper_swirl` | 5 | no | Several overlapping ghostly whispers circling in the dark, unintelligible, horror |
| `mail_slot` | 1 | no | Metal mail slot flap on a front door creaking open and snapping shut |
| `landline_ring` | 5 | no | Old 1990s landline telephone ringing loudly in a dark empty house |
| `phone_buzz` | 2 | no | Smartphone vibrating on a wooden table |
| `intercom` | 1 | no | Old apartment intercom button press with a crackle of static |
| `tv_on` | 2 | no | Old CRT television switching on by itself with a high whine and loud static hiss |
| `thunder` | 6 | no | Close thunder crack followed by a long deep rolling rumble |
| `rain_window` | 10 | yes | Heavy rain on a window and a wooden roof at night, steady and seamless |
| `house_tone` | 10 | yes | Quiet old house at night, low room tone, faint distant wind, very subtle |
| `wind_howl` | 8 | no | Wind howling and moaning around an old wooden house at night |
| `breaker_off` | 2 | no | Electrical breaker tripping, power cutting out with a heavy thunk and dying hum |
| `breaker_on` | 3 | no | Heavy electrical breaker lever thrown, fluorescent lights buzzing and flickering back on |
| `basement_tone` | 10 | yes | Dark damp basement ambience, low hum of an old boiler, slow water drips, unsettling |
| `creature_click` | 4 | no | Inhuman wet clicking and guttural croaking sound from a dark corner, horror creature |
| `creature_scream` | 3 | no | Horrifying distorted inhuman scream, close and loud, horror jumpscare |
| `jumpscare` | 2 | no | Cinematic horror jumpscare hit: a sharp dissonant orchestral stab with a deep sub impact |
| `drag` | 5 | no | A body being dragged slowly across old wooden floorboards |
| `bone_crack` | 3 | no | Bones cracking and popping, a neck slowly bending the wrong way |
| `heartbeat` | 4 | yes | Fast heavy muffled heartbeat, close, seamless |
| `drone_dread` | 10 | yes | Low ominous horror drone with slowly swelling dissonant strings, seamless |
| `sting_reveal` | 4 | no | Sudden horror reveal sting: screeching violins and a deep boom |
| `child_giggle` | 3 | no | A child giggling softly in an empty house, echoing, creepy |
| `church_bell` | 6 | no | A distant church bell tolling once at midnight, long decay |
| `dawn_birds` | 10 | no | Early morning birds chirping softly at dawn after rain |
| `car_pass` | 6 | no | A car slowly passing outside in the rain at night |
| `dog_distant` | 5 | no | A distant dog barking frantically at night, then suddenly whimpering and going silent |
| `faucet_drip` | 6 | yes | A kitchen faucet dripping slowly in a silent room |
| `chair_scrape` | 2 | no | A wooden chair scraping slowly across a kitchen floor in another room |
| `glass_break` | 2 | no | A glass jar falling off a shelf and shattering on a kitchen floor |
| `flashlight` | 1 | no | Flashlight switch clicking on |
| `sheet_rustle` | 3 | no | A bedsheet rustling as someone moves underneath it, very close |
| `walkie_static` | 3 | no | Walkie-talkie squelch and static burst |
| `music_box` | 12 | yes | A slow, out of tune music box lullaby in a minor key, eerie and sad |
