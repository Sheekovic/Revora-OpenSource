using System;

namespace Revora.Game.Features
{
    // Original Revora wording. Persisted sessions bind to this immutable version.
    internal static class QuizQuestionsV1
    {
        internal sealed class Question {
            internal readonly string Text;
            internal readonly string[] Choices;
            internal readonly int Correct;
            internal Question(string text,int correct,params string[] choices) {Text=text;Correct=correct;Choices=choices;}
        }
        internal static readonly Question[] All={
            new Question("Which city is home to the Twin City market square?",0,"Twin City","Ape City","Desert City","Phoenix Castle"),
            new Question("How many different tactic tokens open the Moon Box route?",2,"Two","Four","Six","Eight"),
            new Question("What does the final Moon Box ghost offer beside Meteors?",1,"A Dragon Ball","A Moon Box","A mount","A rune stone"),
            new Question("Which token joins the six tactic tokens into one?",3,"Meteor","Chi Token","Agate Badge","Soul Jade"),
            new Question("How many floors await challengers in the Power Arena?",2,"Three","Five","Seven","Nine"),
            new Question("Who rewards a cleared Power Arena floor?",1,"The City Guard","The floor's Agate","A merchant","A stable keeper"),
            new Question("How many earned Agate Badges mark the Arena badge achievement?",3,"Three","Six","Nine","Twelve"),
            new Question("How many fallen soldiers must you aid for the Desert Guardian?",0,"Three","Five","Eight","Sixteen"),
            new Question("Which city watches over the desert quest's fallen soldiers?",1,"Ape City","Desert City","Twin City","Phoenix Castle"),
            new Question("What cancels your inspection of a fallen soldier?",2,"Waiting quietly","Standing still","Walking away","Remaining nearby"),
            new Question("Who begins the Demon Exterminators hunt?",3,"Desert Guardian","Maggie","Wang","Cloud Saint"),
            new Question("What is the first quarry in Revora's Demon Exterminators hunt?",0,"Birdmen","Pheasants","Tough Horns","City Guards"),
            new Question("How many Birdmen begin the four-stage demon hunt?",1,"300","600","900","1200"),
            new Question("Where does Maggie send Moon Box seekers first?",2,"A stable","A prison","A tactic trial","A shop"),
            new Question("What must both local Arena opponents do before fighting?",3,"Buy a title","Join a guild","Become a guard","Accept the match"),
            new Question("What is the minimum level for the local Qualifier?",0,"40","70","100","130"),
            new Question("Which action counts as entering a local Arena fight?",1,"Watching","Entering your assigned match","Opening the rankings","Standing in a city"),
            new Question("What does a CTF runner carry back to earn capture credit?",2,"A Moon Box","A saddle","A flag","A pickaxe"),
            new Question("How many choices does each of my quiz questions offer?",3,"One","Two","Three","Four"),
            new Question("How many seconds do you have for one quiz question?",0,"30","45","60","90"),
            new Question("A merchant has twelve arrows and sells five. How many remain?",1,"Five","Seven","Nine","Eleven"),
            new Question("Four travelers each carry three lanterns. How many lanterns is that?",2,"Seven","Nine","Twelve","Sixteen"),
            new Question("A journey starts at noon and lasts two hours. When does it end?",3,"Ten in the morning","Noon","One in the afternoon","Two in the afternoon"),
            new Question("Which compass direction lies opposite north?",0,"South","East","West","Northeast")
        };
    }
}
